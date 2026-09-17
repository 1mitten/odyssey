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
    public sealed partial class PawnFigureDirector : IDisposable
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
        /// Work every figure in this style regardless of the job it is doing, or -1 to let the job
        /// decide. **For contact sheets. The game never sets it.**
        ///
        /// <para>It exists because a style can now outrun the simulation. <see cref="WorkStyle"/>
        /// holds a builder's hammer, and nothing in the game builds — there is no job index for
        /// <see cref="WorkStyle.IndexForJob"/> to map, so without this the stroke could not be
        /// photographed at all, and a pose that cannot be photographed cannot be settled. Every
        /// angle in this project that is right is right because somebody looked at it.</para>
        ///
        /// <para>A field rather than a fake job, which was the alternative and is worse: a
        /// <c>JobHandle.Build</c> that no driver runs would put a lie in the contract, show up in
        /// the inspect pane as a job a colonist is not doing, and have to be unpicked when
        /// building is really written. This touches nothing outside presentation and is one
        /// assignment for the harness to make.</para>
        /// </summary>
        public int StyleOverride { get; set; } = -1;

        /// <summary>
        /// Draw every figure in the middle of this gesture, whatever the snapshot says. **For
        /// contact sheets. The game never sets it.**
        ///
        /// <para>A lift is over in eight-tenths of a second and starts on an instant nothing can
        /// predict, so waiting for one to photograph it means either catching it by luck or
        /// slowing the world down — and slowing the world down turns the pause inference on, which
        /// stops the very clock being photographed.</para>
        /// </summary>
        public PawnGesture? ForceGesture { get; set; }

        /// <summary>
        /// Hold every gesture at this phase rather than letting its clock run. **Harness only**,
        /// and the exact counterpart of <see cref="HeldPhase"/> for the stroke.
        ///
        /// <para>Sampling a clock catches whatever phase the frames happen to land on, which for a
        /// one-shot means the bottom of the motion — the one moment worth judging — is as likely
        /// as not to fall between two pictures.</para>
        /// </summary>
        public float? HeldGesturePhase { get; set; }

        /// <summary>
        /// Put every figure on a wall in this direction, whatever the world says. **Harness only.**
        ///
        /// <para>A climb happens where a shaft has been dug, which on a wooded board is nowhere
        /// until somebody has spent a morning mining one, and it lasts under a second when it does.
        /// Photographing one by waiting for it means generating a map with rock in it, digging a
        /// hole and following a colonist down — a great deal of apparatus to look at a pose. This
        /// is <see cref="ForceGesture"/>'s counterpart and the same bargain.</para>
        ///
        /// <para>What a forced sheet can settle and what it cannot is worth stating, because the
        /// difference is not obvious: it settles the <em>figure</em> — boots below the hips and
        /// apart, knees bent alternately, arm and leg on opposite sides rising together. It cannot
        /// settle the figure against the rock, because with this set there is no rock. That half
        /// was settled when the lean landed, against a real shaft.</para>
        /// </summary>
        public Vector3? ForceClimbFace { get; set; }

        /// <summary>
        /// Hold every climb at this point in its cycle rather than reading it off the step's own
        /// progress. **Harness only**, and the counterpart of <see cref="HeldGesturePhase"/>.
        /// </summary>
        public float? HeldClimbPhase { get; set; }

        /// <summary>
        /// Treat every figure as being this far into the water, whatever cell it is standing in.
        /// **Harness only**, and the counterpart of <see cref="ForceClimbFace"/>.
        ///
        /// <para>Needed for the same reason a forced climb face is: the pose is the one thing about
        /// this that no test can judge, and arranging a real colonist to walk into a real stream at
        /// the moment a camera is pointed at it is a great deal of scaffolding to photograph a
        /// shape. Forcing the weight is the whole of what the water does to a figure, so a picture
        /// taken this way is the same picture — see <c>SwimCheck</c>.</para>
        /// </summary>
        public float? ForceSwim { get; set; }

        /// <summary>
        /// How far the last drawn crouch took the hips below where the animation had them, in
        /// metres.
        ///
        /// <para>The crouch's answer to <see cref="MeasuredBladeGap"/>, and it exists for the same
        /// reason: a figure photographed from three-quarters can be read as stooping when it is
        /// barely moving, and the difference between a lift and a nod is a number nobody can
        /// estimate off a picture. Zero here means the legs never bound and the pose did nothing.</para>
        /// </summary>
        public float MeasuredCrouchDrop { get; private set; }

        /// <summary>
        /// How many figures the crouch actually posed on the last pass, and how many it skipped for
        /// want of a pelvis. Diagnostic: a rig that is not configured Humanoid binds no bones at
        /// all, which is silent — that colonist simply goes on standing while the rest stoop.
        /// </summary>
        public int CrouchedFigures { get; private set; }

        /// <summary>See <see cref="CrouchedFigures"/>. Non-zero means some rig has no legs.</summary>
        public int LeglessFigures { get; private set; }

        /// <summary>
        /// How far the worst-placed boot finished from the foothold it was sent to this pass, in
        /// metres. Zero is a foot on the rock.
        ///
        /// <para>The climb's <see cref="MeasuredBladeGap"/>. <see cref="TwoBoneIk"/> straightens
        /// towards a target it cannot reach and stops, so a foothold asked for beyond the leg draws
        /// as a plausible enough pose and is only ever caught by being stated in centimetres.</para>
        /// </summary>
        public float MeasuredFootReach { get; private set; }

        /// <summary>
        /// How far the off forearm sits above the working one at the moment of the blow, in metres.
        /// Positive is over, negative is under.
        ///
        /// <para>The grip's answer to <see cref="MeasuredBladeGap"/>, and needed for the same
        /// reason. Which arm passes over which cannot be read off the pictures this project takes:
        /// in profile the two arms are one behind the other, and from the front the tree is in the
        /// way of the very place they cross. It is a number, so it should be reported as one.</para>
        ///
        /// <para>Measured at the forearms rather than the hands because the hands are both on the
        /// haft within a few centimetres of each other by construction — where they *cross* is the
        /// forearm, which is also the part whose mesh intersects the other arm's.</para>
        /// </summary>
        public float MeasuredOffArmAbove { get; private set; }

        /// <summary>How far apart the two forearms are at all, in metres. Under about 0.15 m they
        /// intersect whatever their relative height. See <see cref="MeasuredOffArmAbove"/>.</summary>
        public float MeasuredArmGap { get; private set; }

        /// <summary>
        /// How far the off hand's palm finished from the line of the haft, in metres. Zero is a
        /// hand on the wood.
        ///
        /// <para>The grip's <see cref="MeasuredBladeGap"/>. A hand seated on the wrist bone — which
        /// is what every hand in this project did until 2026-09-16 — reads as "near the axe" in any
        /// photograph and is unmistakable the moment it is stated in centimetres.</para>
        /// </summary>
        public float MeasuredGripGap { get; private set; }

        /// <summary>How much further away the haft is than the off arm is long, in metres.
        /// Positive means no solver can reach it and the stroke must bring the tool nearer.</summary>
        public float MeasuredGripOverreach { get; private set; }

        /// <summary>
        /// How far the worst-placed tool had turned in its fist since it was last put right, in
        /// degrees. **Zero is the only acceptable value**, and it is what "a tool never spins"
        /// means when it is written as a number rather than as an instruction.
        /// </summary>
        public float MeasuredToolDrift { get; private set; }

        /// <summary>Where the working hand is on the haft this frame, 0 butt, 1 head. Diagnostic.</summary>
        public float MeasuredGripAt { get; private set; }

        /// <summary>How far the off hand's target is from the off shoulder, in metres. Diagnostic.</summary>
        public float MeasuredGripSpan { get; private set; }

        /// <summary>Where the off hand's target is in the figure's own frame: x right, y up,
        /// z forward, metres. Diagnostic — a span alone cannot say which way.</summary>
        public Vector3 MeasuredGripLocal { get; private set; }

        /// <summary>The same for the working hand's palm. Diagnostic.</summary>
        public Vector3 MeasuredPalmLocal { get; private set; }

        /// <summary>Which style a pawn is worked in, the override first. See <see cref="StyleOverride"/>.</summary>
        int StyleFor(int jobDef) =>
            StyleOverride >= 0 && StyleOverride < Styles.Length
                ? StyleOverride
                : WorkStyle.IndexForJob(jobDef);

        /// <summary>
        /// Where the off hand grips, as a fraction of the haft, relative to the main hand.
        ///
        /// Both fists at the butt (owner, 2026-09-16), so this is small: just far enough up the
        /// haft that the hands are side by side rather than in the same place.
        /// </summary>
        // ButtFraction and SlideFraction moved to WorkStyle; see Styles above.

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

        /// <summary>
        /// Which way a drawn figure is actually facing, in degrees.
        ///
        /// <para><b>Not derivable from the snapshot, and that cost a contact sheet.</b> The obvious
        /// way to find a figure's bearing is the line from its cell to its next cell — which is
        /// zero for a pawn that is standing still, and a pawn standing still is exactly what a pose
        /// harness photographs. <c>GestureCheck</c> fell back to a fixed bearing and shot a crouch
        /// head-on, which is the one view in which a vertical motion cannot be seen at all.</para>
        ///
        /// <para>The figure knows, because the yaw it is drawn at is eased and remembered here
        /// across exactly the frames in which the snapshot has forgotten it.</para>
        /// </summary>
        public bool TryGetFacing(PawnId id, out float yaw)
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                if (_figures[i].Pawn != id.Value || _figures[i].Transform == null) continue;

                // The transform's own rotation, not the Yaw field it was derived from. They are
                // not the same thing and the difference cost two contact sheets: Yaw is the
                // director's eased bearing, and what a figure is actually drawn facing is that
                // turned by whatever the gait clip's own root rotation adds. A camera aimed
                // square across Yaw came out square behind the colonist.
                yaw = _figures[i].Transform.eulerAngles.y;
                return true;
            }

            yaw = 0f;
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

        readonly Look?[] _looks;
        readonly int _usableLooks;
        readonly ModuleCatalogue? _catalogue;

        /// <summary>
        /// Where a colonist's colours come from. Null draws every figure in the pack's own paint,
        /// which is what a harness that never set one gets.
        /// </summary>
        public ColonistMaterials? Materials { get; set; }

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

        /// <summary>
        /// Raised on the frame a tool's blow lands, with the work style (index into
        /// <see cref="WorkStyle.All"/>) and where the edge struck, in world space.
        ///
        /// The same moment the chips fly, published as an event because audio is not this
        /// director's business and neither is anything else that wants the instant: the sound of
        /// an axe and the sound of a pick are the same blow on a different tool, and the one
        /// place that knows when a blow lands should not have to know everything that follows
        /// it. Raised whether or not <see cref="Chips"/> exists, because a clone with no usable
        /// particle shader still has working ears.
        /// </summary>
        public event Action<int, Vector3>? BlowLanded;

        /// <summary>
        /// Whether the last <see cref="Sync"/> saw a world that was advancing.
        ///
        /// Read off the snapshot, never inferred, and public because it is the one bit of state
        /// that decides whether anything on the board moves at all: a harness that photographs a
        /// frozen colonist can say which of the two reasons it is looking at.
        /// </summary>
        public bool Running { get; private set; } = true;

        readonly List<Figure> _figures = new List<Figure>();
        readonly Dictionary<int, Figure> _byPawn = new Dictionary<int, Figure>();
        readonly List<int> _retired = new List<int>();

        /// <summary>True when there is at least one usable face, so figures can be made at all.</summary>
        public bool Enabled => _usableLooks > 0;

        /// <summary>
        /// The size of the face lottery: every colonist row the catalogue has, holes included.
        ///
        /// Not the number of *usable* faces. This is the index space the appearance book deals in
        /// and the instanced renderer buckets by, and the three must be the same number or a
        /// colonist changes identity on crossing the figure cap. See
        /// <see cref="ColonistAppearanceBook"/>, which explains how they used to differ.
        /// </summary>
        public int LookCount => _looks.Length;

        /// <summary>How many of those rows actually resolved to art this session.</summary>
        public int UsableLookCount => _usableLooks;

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
                          : figure.ClimbFace.ToString("0.0"))
                      .Append(" legs ").Append(figure.LegLength <= 0f
                          ? "NONE - no leg bones bound"
                          : figure.LegLength.ToString("0.00") + " m");
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
            _catalogue = catalogue;
            _looks = LooksFrom(catalogue);
            for (int i = 0; i < _looks.Length; i++) if (_looks[i] != null) _usableLooks++;
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
        /// <summary>
        /// One slot per colonist row of the catalogue, in the catalogue's own order, with
        /// <c>null</c> where the art did not resolve.
        ///
        /// <para><b>The holes are the point.</b> This used to skip unusable rows and compact the
        /// survivors, which quietly made look <c>i</c> mean a different body here than it meant to
        /// the instanced renderer — so with three packs of four installed, every colonist would
        /// change face on crossing the figure cap, and the fault would be hunted in the
        /// simulation. Keeping the slot preserves the index space; a pawn whose face is a hole is
        /// simply not drawn as a figure and takes the baked path instead, which degrades that one
        /// colonist rather than re-dealing the colony.</para>
        /// </summary>
        static Look?[] LooksFrom(ModuleCatalogue? catalogue)
        {
            if (catalogue == null) return Array.Empty<Look?>();

            List<ModuleEntry> rows = catalogue.FindFamily(ModuleIds.ColonistBase);
            var looks = new Look?[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                ModuleEntry row = rows[i];
                if (row.prefab == null) continue;
                LocomotionEntry[] gaits = Gaits(row);
                if (gaits.Length == 0) continue;

                looks[i] = new Look
                {
                    Prefab = row.prefab,
                    Scale = row.scale,
                    Gaits = gaits,
                    Speeds = GroundSpeeds(gaits, row.scale),
                };
            }
            return looks;
        }

        /// <summary>
        /// Who every colonist is: which face, and what colour their skin, hair and clothes are.
        ///
        /// <para>Set before the first <see cref="Sync"/> and then left alone. It must be the
        /// <b>same object</b> the instanced renderer holds, not an equal one — that is the whole
        /// reason the book exists, and a test asserts the identity. Two drawers that computed the
        /// answer separately would put a different person on screen the moment a colonist crossed
        /// the figure cap, and the fault would be hunted in the simulation.</para>
        ///
        /// <para>A harness that never sets one gets a book dealt from seed 0 over the same number
        /// of faces, so an editor tool still draws a varied cast without having to know this type
        /// exists. That is safe precisely because the derivation is pure: two books with the same
        /// seed and the same face count give the same answers, object identity or not. Identity
        /// still matters once overrides exist, which is why the game hands one object to both.</para>
        /// </summary>
        public ColonistAppearanceBook Appearances
        {
            get => _appearances ??= new ColonistAppearanceBook(0u, _looks.Length);
            set => _appearances = value;
        }

        ColonistAppearanceBook? _appearances;

        int LookFor(PawnId pawn) => Appearances.LookFor(pawn.Value);

        /// <summary>
        /// How far the sole sits below the ankle, on the figure whose boot is thickest.
        ///
        /// Printed by the contact sheets. A rig that answers zero is one whose feet are not bound,
        /// and a number far from a tenth of a metre is one worth looking at rather than trusting.
        /// </summary>
        public float MeasuredSoleOffset { get; private set; }


        /// <summary>True when this pawn's face resolved to art and a figure can be built for it.</summary>
        bool CanDraw(PawnId pawn)
        {
            if (_looks.Length == 0) return false;
            int look = LookFor(pawn);
            return (uint)look < (uint)_looks.Length && _looks[look] != null;
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

            // Is the world actually running? The snapshot says so — see WorldSnapshot.GameSpeed.
            //
            // It used to be inferred from the tick standing still, with a quarter of a second of
            // grace, and both halves of the owner's report came out of that. The grace is fifteen
            // frames at sixty, and measured against the axe's 1.15 s stroke it is 24% of a swing
            // that ran on after the player pressed space — "some even carry on for a moment". And
            // because the inference only ever gated the *swing*, everything else went on easing:
            // a walking colonist's gait blend, measured, goes from 73.5% walk to 99% idle in ten
            // frames (0.167 s), which is what reads as the figures resetting to a default pose.
            //
            // Nothing is inferred now, and nothing eases while the world is stopped. Running is
            // the frame's whole clock: pass it a paused snapshot and every figure holds the pose
            // it was drawn in until the world moves again. See Pose and Blend.
            Running = snapshot.Running;
            bool running = Running;

            // Read before the early return, so a clone with no faces still reports the truth and
            // a harness looking at a motionless board is not told the world is running.
            if (!Enabled) return;

            // The chips as well, or wood thrown a frame before the pause would go on falling
            // while everything that threw it stood still. They are world-simulated by Unity, so
            // nothing here steps them; the speed is what Unity steps them at.
            if (Chips != null) Chips.Running = running;

            // Down to the bottom of the landscape, on the same terms the world is drawn on: the
            // terraced surface spans several layers, and a figure walking a low one used to be
            // culled with the ground it stood on. See WorldRenderModel.LowestOutdoorLayer.
            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(
                activeLayer, World != null ? World.LowestOutdoorLayer : int.MaxValue));

            // And up to the highest layer the world is drawn on. A figure belongs wherever its
            // surroundings are visible: the owner's report was that a colonist mining one layer up
            // could not be seen at all, because this cull was against the active layer while the
            // rock around that colonist was being x-rayed perfectly well.
            int highest = slice.HighestVisibleLayer(activeLayer, snapshot.Size.SizeY);
            var pawns = snapshot.Pawns;

            for (int i = 0; i < pawns.Length && Drawn.Count < MaxFigures; i++)
            {
                CellRef cell = pawns[i].Cell;
                if (cell.Y < lowest || cell.Y > highest) continue;

                // A face that did not resolve is not drawn here at all: the pawn falls through to
                // the baked path, which will draw whatever that row does resolve to (a marker, if
                // nothing). Skipping is what keeps a missing row a one-colonist problem.
                if (!CanDraw(pawns[i].Id)) continue;

                Vector3 position = PawnPose.Of(pawns[i], tickAlpha, movePerTick, out Vector3 heading, World);
                Figure figure = Lease(pawns[i].Id, position);
                Pose(figure, in pawns[i], position, heading, deltaTime, running);
                if (figure.Speed > FastestSpeed) FastestSpeed = figure.Speed;
                Drawn.Add(pawns[i].Id.Value);
            }

            Retire();
            ApplyFooting();
            ApplyWorkPose();
        }

        /// <summary>Advance every live figure's animation. Separate from posing so an editor
        /// tool can step the clock deliberately rather than relying on a running player.</summary>
        public void Evaluate(float deltaTime)
        {
            for (int i = 0; i < _figures.Count; i++)
                if (_figures[i].Pawn >= 0) _figures[i].Graph.Evaluate(deltaTime);
            ApplyFooting();
            ApplyWorkPose();
            // The chips as well: under the player loop Unity steps them, and in an editor tool
            // with no player loop nothing does, so a photographed blow would throw wood that
            // never moved. Stepping them here costs the game nothing, because the game never
            // calls Evaluate at all.
            Chips?.Evaluate(deltaTime);
        }


        /// <summary>
        /// Work out where a sleeping colonist's body goes: which way it lies, about what point, and
        /// on what surface.
        ///
        /// <para><b>The bed is looked up rather than carried on the view.</b> The pawn is standing
        /// in the bed's own cell and presentation already knows which way every bed faces and how
        /// high its mattress is, so a field on <c>PawnView</c> saying so would be a second copy of
        /// an answer this side already has — and the one that would go stale.</para>
        ///
        /// <para><b>No bed is not a failure case.</b> A colonist who could not reach one lies down
        /// where it is, along whatever it was last facing; that is the <c>SleptOnGround</c> memory
        /// made visible, and it is the owner's own second ask (2026-09-18: "lies on the bed and also
        /// lies on the floor").</para>
        /// </summary>
        void AimSleep(Figure figure, in PawnView pawn)
        {
            int index = World != null && World.Size.Contains(pawn.Cell.X, pawn.Cell.Z, pawn.Cell.Y)
                ? World.Size.Index(pawn.Cell.X, pawn.Cell.Z, pawn.Cell.Y)
                : -1;

            int head = index >= 0 && World != null ? World.BedHeadAt(index) : -1;
            if (head >= 0 && World != null)
            {
                CellRef at = World.Size.FromIndex(head);
                int facing = World.BedFacing(head);

                // The bed's own origin and its own facing, from the same place the bed itself is
                // drawn from — so a sleeper cannot lie across a bed that has been turned.
                Vector3 origin = GroundRelief.Lift(BedShape.Origin(at.X, at.Z, at.Y, facing));
                figure.SleepCentre = origin;
                figure.SleepSurfaceY = origin.y + BedShape.MattressTop;
                figure.SleepAlong = new Vector3(Directions.DeltaX[facing], 0f, Directions.DeltaZ[facing]);
                return;
            }

            Vector3 floor = GroundRelief.Lift(CellMetrics.FloorCentre(pawn.Cell));
            figure.SleepCentre = floor;
            figure.SleepSurfaceY = floor.y;

            // Whatever it was facing when it lay down. Held rather than recomputed, so a colonist
            // asleep on the ground does not swing round as the yaw eases.
            Vector3 along = Quaternion.Euler(0f, figure.Yaw, 0f) * Vector3.forward;
            if (along.sqrMagnitude > 1e-6f) figure.SleepAlong = along;
        }

        /// <summary>
        /// Force every figure to a sleep weight, for a harness and a contact sheet. Null is the
        /// game's own answer, which is what the pawn says.
        /// </summary>
        public float? ForceSleep { get; set; }

        /// <summary>Add a world-space pitch to a bone, leaving the rest of its pose alone.</summary>
        static void Pitch(Transform? bone, Vector3 axis, float degrees)
        {
            if (bone == null) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        /// <summary>
        /// How fast a figure is walking, from where the simulation put it last frame and where it
        /// has put it now.
        ///
        /// <para>Pulled out of <see cref="Pose"/> because it is the number that decides which gait
        /// plays, and because a figure cannot be built outside a running editor — a static over
        /// two positions can be driven across a pause by an ordinary test, and the pose it feeds
        /// cannot.</para>
        ///
        /// <para><b>Differenced against where the simulation last put the pawn</b>, not against
        /// where the figure was last drawn. Those parted company the moment a working figure began
        /// stepping up to its tree: a metre and a half of step over a quarter of a second is six
        /// metres a second, which would have thrown a standing woodcutter into a sprint cycle on
        /// the spot.</para>
        ///
        /// <para><b>Ground speed, so the vertical part does not count.</b> A colonist climbing out
        /// of a shaft covers three metres without going anywhere: counted whole, that is a walk
        /// cycle playing while the figure rises through the air with nothing under its feet.</para>
        ///
        /// <para><b>No frame, no answer.</b> A delta of nothing is a frame in which the pawn had
        /// no opportunity to move, and it says nothing whatever about how fast the figure is
        /// going — so the last answer stands. That single line is most of the pause fix. It used
        /// to read the absence of movement as a measurement of nought and smooth towards it, and
        /// measured against the real gait speeds that carried a walking colonist from 73.5% walk
        /// weight to 99% idle in ten frames, 0.167 s: a figure that visibly snapped to a standing
        /// pose the instant the player pressed space. Held instead, it keeps the stride it was
        /// drawn in and picks the walk straight back up when the world moves again.</para>
        /// </summary>
        public static float ObserveSpeed(float previous, Vector3 simPosition, Vector3 position,
            float deltaTime, bool settled)
        {
            if (!settled) return 0f;
            if (deltaTime <= 1e-5f) return previous;

            Vector3 moved = position - simPosition;
            moved.y = 0f;

            // One frame of a lost path or a slice change can jump a pawn further than any gait
            // covers. Smoothing keeps a single frame from throwing the figure into a sprint.
            return Mathf.Lerp(previous, moved.magnitude / deltaTime, SpeedSmoothing);
        }

        /// <summary>How much of a frame's measured speed the figure's smoothed speed takes.</summary>
        public const float SpeedSmoothing = 0.35f;

        void Pose(Figure figure, in PawnView pawn, Vector3 position, Vector3 heading,
            float frameTime, bool running)
        {
            // **The one clock every ease in this method runs on, and it stops when the world
            // does.** A pause should hold each figure on the frame it is on and then carry on
            // from there, which is what a delta of exactly nothing gives for free: MoveTowards
            // with a step of zero is the identity, so the work weight, the climb lean, the swing,
            // the gesture and the turn all keep the value they had, and the frame after the
            // player starts the world again continues from it rather than restarting.
            //
            // Placement is deliberately *not* on this clock. A figure still has to be put
            // somewhere — a pawn newly leased because the player scrolled the slice while paused
            // has no drawn position at all — so everything below that computes a position from
            // state rather than advancing it is left alone.
            float deltaTime = running ? frameTime : 0f;

            // Work eases in and out rather than switching, and the axe is in the hand for exactly
            // as long as the pose is worth anything. See WorkEaseSeconds.
            float step = WorkEaseSeconds > 1e-3f ? deltaTime / WorkEaseSeconds : running ? 1f : 0f;
            figure.WorkWeight = Mathf.MoveTowards(figure.WorkWeight, pawn.Working ? 1f : 0f, step);

            // The swing's own clock, which runs only while there is work. Freezing it between
            // jobs rather than letting it free-run means a colonist's first blow at a new tree
            // is a first blow, not whatever part of a stroke the wall clock happened to be in.
            if (pawn.Working && running) figure.SwingClock += deltaTime;
            else if (!pawn.Working && figure.WorkWeight <= 0f) figure.SwingClock = 0f;

            // The one-shot gestures, started by a serial that has moved rather than by a state
            // that is true. See PawnView.GestureSerial: the view reports the *last* gesture
            // permanently, so that a frame cannot miss one, which means "is it Lift?" is never the
            // question — "is it a Lift I have not already drawn?" is.
            //
            // **This is the only place the gesture clock moves.** ApplyWorkPose runs at the end of
            // both Sync and Evaluate, and is harmless twice only because it re-derives the same
            // pose from the same state. A clock advanced inside it would run at double speed under
            // the player loop and single speed in an editor harness that steps the graph by hand —
            // which is to say, wrong in the game and right in every picture taken of the game.
            if (pawn.GestureSerial != figure.SeenSerial)
            {
                // First sighting records and poses nothing. A figure leased for a colonist who has
                // been hauling for an hour would otherwise open with a lift it never made, as would
                // every colonist on the board on the first frame after a load.
                if (figure.SeenSerial >= 0 && pawn.Gesture != PawnGesture.None)
                {
                    figure.Gesture = pawn.Gesture;
                    figure.GestureClock = 0f;
                }

                figure.SeenSerial = pawn.GestureSerial;
            }

            if (figure.Gesture != PawnGesture.None)
            {
                // Work wins. Nothing in the game can pick something up and swing an axe at the same
                // time, but the two poses write the same bones, and a gesture left running under a
                // work pose would be a fight rather than a blend.
                if (pawn.Working) figure.Gesture = PawnGesture.None;
                else if (running)
                {
                    figure.GestureClock += deltaTime;
                    if (GestureOf(figure.Gesture).Finished(figure.GestureClock))
                        figure.Gesture = PawnGesture.None;
                }
            }

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
            //
            // **Strictly vertical, and that test is the whole of it** (owner, 2026-09-16). There
            // are now two ways to change layer and they want opposite things:
            //
            //   * a LADDER joins a cell to the one directly above it — same x and z — and is
            //     climbed. `SurfacePasses` stamps them one cell to one cell, so the geometry is
            //     the signal and no new contract is needed.
            //   * a HOP is a jump up onto the block next door, or a drop off it: one cell across
            //     as well as one layer up. Posing that as a climb is what the owner reported as
            //     the animation looking wrong, and it needs no pose at all — a hop has real
            //     horizontal travel, so the gait already walks the figure up onto the block.
            //
            // A stair sorts itself out by the same test: a stairwell moves across as well as up.
            // If lifts ever land this has to become a question about the connector's KIND rather
            // than its geometry, because a lift is vertical and you stand in it.
            bool straightUp = pawn.NextCell.X == pawn.Cell.X && pawn.NextCell.Z == pawn.Cell.Z;
            figure.ClimbPhase = pawn.MovePercent > 0 && pawn.NextCell.Y != pawn.Cell.Y && straightUp
                ? HeldClimbPhase ?? Mathf.Clamp01(pawn.MovePercent * 0.01f)
                : -1f;

            // And WHAT it is climbing. A colonist goes up the edge of the block beside the hole,
            // not up the middle of the hole: drawn at the cell centre it is a person levitating
            // through clear air, which is what the owner saw. The simulation now refuses to lay a
            // connector where there is no block (MineWorkGiver's HasWallBeside), so this should
            // always find one — and where it does not, the figure simply stays where it was, which
            // is the behaviour before any of this existed.
            // Forced first, and completely: a harness that set only the direction would still be
            // waiting for a real vertical step to give it a phase, and there is never going to be
            // one on a board with no shaft in it.
            figure.ClimbFace = Vector3.zero;
            if (ForceClimbFace.HasValue)
            {
                figure.ClimbPhase = HeldClimbPhase ?? 0f;
                figure.ClimbFace = ForceClimbFace.Value;
                figure.LastClimbFace = figure.ClimbFace;
                heading = figure.ClimbFace;
            }
            else if (figure.ClimbPhase >= 0f)
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

            // Swimming: is this colonist in water, and how far into looking like it.
            //
            // The target is blended over the step by `WaterLine.Weight`, so a colonist wading in
            // off a bank is half a swimmer half way across the step and the pose comes on at
            // exactly the rate the drawn height rises. Snapping either one on at the water's edge
            // moves the figure nearly two metres in a frame; snapping only one of them puts a
            // prone figure on the bank, or an upright one afloat.
            //
            // **Presentation only** (owner, 2026-09-17: "float is how it looks; shallow stays
            // crossable"). Nothing here reads back into the simulation: a colonist in shallow
            // water carries what it was carrying, works where it was working, and pays the third
            // speed the cost class has always charged. The helpless-swimmer rules are deep water's
            // and are not built — docs/design/20-swimming-and-water.md.
            float afloat = ForceSwim ?? WaterLine.Weight(World, pawn.Cell, pawn.NextCell,
                Mathf.Clamp01(pawn.MovePercent * 0.01f));

            // Forced weight is taken whole rather than eased towards, so a harness that sets it
            // gets the pose on the frame it asks rather than a third of a second later — the same
            // reason ForceClimbFace assigns the phase outright.
            figure.SwimWeight = ForceSwim.HasValue
                ? afloat
                : SwimPose.Settle(figure.SwimWeight, afloat, deltaTime);
            if (running && figure.SwimWeight > 0.001f) figure.SwimClock += deltaTime;

            // Asleep, and where. A bed decides which way the body lies and how high off the floor;
            // with no bed the colonist lies where it dropped, facing wherever it last faced, which
            // is the SleptOnGround case and is drawn rather than left standing.
            figure.SleepWeight = ForceSleep.HasValue
                ? ForceSleep.Value
                : SleepPose.Settle(figure.SleepWeight, pawn.Asleep ? 1f : 0f, deltaTime);
            if (running && figure.SleepWeight > 0.001f) figure.SleepClock += deltaTime;
            if (figure.SleepWeight > 0.001f) AimSleep(figure, in pawn);

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

            // Speed from displacement, which is right at every game speed and needs to know
            // nothing about ticks. A figure that has just been leased has no previous position
            // worth differencing, hence Settled.
            bool settled = figure.Settled;
            figure.Speed = ObserveSpeed(figure.Speed, figure.SimPosition, position, deltaTime, settled);
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
                WorkStyle style = Styles[StyleFor(pawn.JobDef)];

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
                if (figure.WorkWeight <= 0.5f) figure.Style = StyleFor(pawn.JobDef);
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

            // Stand on the ground rather than merely above it.
            //
            // GroundRelief lifts everything that stands on the board and shears only the board
            // itself — "a person standing on a hillside stands up". That is right about a
            // *position* and was never the whole answer: a figure lifted onto a slope and left
            // bolt upright meets it on one heel, with the downhill foot in the air and the uphill
            // one buried. The lift puts the colonist in the right place; the lean puts it in the
            // right attitude, and Footing decides how much of the slope it takes.
            //
            // Composed on the left of the bearing, so the figure yaws in the world and then leans
            // with the hill. The other order leans it in its own frame, which turns the lean into
            // a roll as it walks in a circle.
            //
            // The swing needs no separate fix and must not be given one: SwingAxis is built from
            // figure.right and figure.forward, so the plane an axe travels in tilts with the body
            // for free, which is what a woodcutter on a slope actually does.
            GroundRelief.SlopeAt(drawn.x, drawn.z, out float slopeX, out float slopeZ);
            Quaternion wanted = Footing.LeanTo(Footing.GroundNormal(slopeX, slopeZ));
            figure.Lean = settled ? Footing.Settle(figure.Lean, wanted, deltaTime) : wanted;

            figure.GroundY = drawn.y - GroundRelief.HeightAt(drawn.x, drawn.z);
            figure.Transform.rotation = figure.Lean * Quaternion.Euler(0f, figure.Yaw, 0f);

            // Laid down last, over everything above, because lying is a statement about the whole
            // figure rather than an adjustment to a standing one: the lean, the heading and the
            // footing all describe a colonist on its feet and none of them means anything once it
            // is on its back. Blended by the weight, so the standing pose is what it eases from.
            if (figure.SleepWeight > 0.001f)
            {
                SleepPose.Place(
                    SleepPose.PostureFor(figure.Pawn), figure.SleepCentre, figure.SleepAlong,
                    figure.SleepSurfaceY, figure.StandingHipHeight, figure.SleepWeight,
                    figure.Transform.position, figure.Transform.rotation,
                    out Vector3 lain, out Quaternion laid);
                figure.Transform.position = lain;
                figure.Transform.rotation = laid;
            }

            Blend(figure, figure.Speed, running);
        }

        /// <summary>
        /// Set the mixer weights for a speed, and the playback rate that keeps feet on the ground.
        ///
        /// Inside the range the gaits cover, a linear blend between the two that bracket the speed
        /// means the blended stride already matches the ground speed, so the clips play at their
        /// authored rate. Only above the fastest gait does the rate have to stretch, and that is
        /// the one case where a figure is genuinely moving faster than any clip was made for.
        ///
        /// <para><b>A rate of nothing is how a pause is held.</b> The graph is played with
        /// <c>DirectorUpdateMode.GameTime</c> and nothing in this game touches
        /// <c>Time.timeScale</c>, so Unity goes on evaluating every figure's clips on wall-clock
        /// frames whatever the simulation is doing — a paused colony breathed, shifted its weight
        /// and swayed. Setting the clip speed to zero is the narrowest possible way to stop that:
        /// it is the same call that is already made on every clip on every frame, so nothing new
        /// can go wrong with it, the clip time simply stops advancing, the pose the graph writes
        /// is the pose it wrote last frame, and the frame the world starts again the rate comes
        /// back and the clip <em>continues</em> rather than restarting. Stopping the graph or
        /// unplaying it would also have to be undone, and would not leave the bones written at
        /// all — the additive work pose is laid over what the graph writes and needs it there.
        /// </para>
        /// </summary>
        void Blend(Figure figure, float speed, bool running)
        {
            Look look = _looks[figure.Look]!;

            // **A swimmer has ground speed and must not walk on it.** The gait reads speed from
            // how far the figure moved this frame, which is the right rule everywhere else and
            // exactly wrong here: a colonist crossing a stream is travelling, so without this the
            // mixer plays a walk cycle and the figure strides along the surface of the water. The
            // speed is faded out with the swim weight rather than zeroed, so a colonist wading in
            // off the bank slows to the idle as it tips over instead of stopping dead a frame
            // before.
            //
            // This is the climb's own fault arriving from the other side. There, a purely vertical
            // step had *no* ground speed, so the mixer played the idle and a colonist went up a
            // shaft standing to attention until the legs were bound. Ground speed is a poor proxy
            // for what the legs are doing, and every pose that is not walking has to say so.
            speed *= 1f - Mathf.Clamp01(figure.SwimWeight);

            GaitBlend blend = GaitBlend.Solve(look.Speeds, speed);
            for (int i = 0; i < look.Gaits.Length; i++)
            {
                figure.Mixer.SetInputWeight(i, blend.WeightOf(i));
                figure.Clips[i].SetSpeed(running ? blend.Rate : 0f);
            }
        }

        /// <summary>
        /// Dress a figure in the colours the pawn borrowing it was dealt.
        ///
        /// <para>Assigns <c>sharedMaterial</c> and never <c>material</c>. The latter silently
        /// instantiates a per-renderer copy that Unity then owns and never collects — the same
        /// class of leak <c>ModuleLibrary.Dispose</c> exists to prevent, arriving once per lease
        /// rather than once per look.</para>
        ///
        /// <para>A body the classifier could not read, or a build with no character shader, gets
        /// its own art back rather than something approximate. That is what keeps an unclassified
        /// colonist looking exactly the way the artist painted it.</para>
        /// </summary>
        void Repaint(Figure figure, PawnId pawn)
        {
            if (Materials == null || figure.Skins.Length == 0) return;

            AppearanceCells? cells = CellsFor(figure.Look);
            ColonistAppearance look = Appearances.For(pawn.Value);

            for (int i = 0; i < figure.Skins.Length; i++)
            {
                SkinnedMeshRenderer skin = figure.Skins[i];
                if (skin == null) continue;

                Material? art = figure.ArtMaterials[i];
                Material? painted = Materials.For(art, cells, look);
                skin.sharedMaterial = painted != null ? painted : art;
            }
        }

        /// <summary>
        /// Dress every live figure again, for when the colours themselves have changed.
        ///
        /// A figure is normally painted once, as it is leased, because its colours are a function
        /// of the pawn wearing it and neither changes while it is on screen. Two things break that
        /// assumption: the contact sheet, which forces a slot to a signal colour and shoots the
        /// same colony again, and — later — an appearance panel, where the player picks a colour
        /// for somebody already standing in front of them.
        /// </summary>
        public void RepaintAll()
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Pawn < 0) continue;
                Repaint(figure, new PawnId(figure.Pawn));
            }
        }

        /// <summary>Which swatches this face's body uses, or null when it was never classified.</summary>
        AppearanceCells? CellsFor(int look)
        {
            if (_catalogue == null) return null;
            List<ModuleEntry> rows = _catalogue.FindFamily(ModuleIds.ColonistBase);
            if ((uint)look >= (uint)rows.Count) return null;
            AppearanceCells cells = rows[look].appearance;
            return cells.Any ? cells : null;
        }

        Figure Lease(PawnId pawn, Vector3 at)
        {
            if (_byPawn.TryGetValue(pawn.Value, out Figure? existing)) return existing;

            // The pool is keyed by face as well as by being free: a figure is a *built* prefab
            // with a graph bound to its own rig, so handing a parked one to a pawn wearing a
            // different face would put the wrong person on screen rather than save any work.
            int look = LookFor(pawn);
            Figure figure = Free(look) ?? Create(look);
            Repaint(figure, pawn);
            figure.Pawn = pawn.Value;
            figure.Settled = false;
            figure.Speed = 0f;
            figure.WorkWeight = 0f;
            figure.SwingClock = 0f;

            // A recycled figure has somebody else's gesture history on it. Forgetting it here is
            // what stops a colonist walking into view playing the last lift the previous tenant of
            // this body made — and, because -1 is "never seen", stops it playing one at all until
            // this pawn genuinely begins a new gesture.
            figure.Gesture = PawnGesture.None;
            figure.GestureClock = 0f;
            figure.SeenSerial = -1;
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
            Look face = _looks[look]!;
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
            figure.Skins = skins;
            figure.ArtMaterials = new Material?[skins.Length];
            for (int i = 0; i < skins.Length; i++) figure.ArtMaterials[i] = skins[i].sharedMaterial;
            BindWorkBones(figure, animator);
            figure.SoleOffset = MeasureSole(figure);
            if (figure.SoleOffset > MeasuredSoleOffset) MeasuredSoleOffset = figure.SoleOffset;
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

    }
}
