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
    /// <see cref="PawnFigureDirector"/>: the per-figure state it keeps.
    ///
    /// <para><c>Figure</c> is one live, animated colonist — its bones, its gait state, its stroke
    /// clock and its fitted tools. <c>FittedTool</c> is one prop already measured into a grip.
    /// Split out of the director on 2026-09-16 because the one file had reached 3,033 lines.</para>
    ///
    /// <para>Nothing here is simulation state. A figure is discarded and re-leased freely; the
    /// pawn it draws owns everything that must survive.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
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

            /// <summary>
            /// The figure's skinned renderers and the material each was built with.
            ///
            /// Kept so a lease can repaint the figure for the pawn borrowing it and hand the art
            /// back when it cannot. The pool is keyed on the face, not on the colours, so the same
            /// body is lent to colonists wearing different clothes and must be repainted on every
            /// lease rather than once at construction.
            /// </summary>
            public SkinnedMeshRenderer[] Skins = Array.Empty<SkinnedMeshRenderer>();
            public Material?[] ArtMaterials = Array.Empty<Material?>();

            /// <summary>
            /// The hair and the beard this figure is wearing, as two renderers parented to the
            /// head bone (<c>docs/design/29-modular-colonists.md</c>, MC5).
            ///
            /// <para><b>Built once with the figure and re-dressed on every lease</b>, exactly as
            /// the materials are and for the same reason: the pool is keyed on the face, so one
            /// body is lent to colonists with different hair. Instantiating a prefab per lease
            /// would put an allocation and a destroy in the middle of a colonist walking on
            /// screen; swapping <c>sharedMesh</c> costs nothing.</para>
            ///
            /// <para>Null on a machine with no licensed art, and disabled whenever the pawn was
            /// dealt no piece — bald and clean-shaven are ordinary outcomes, not failures.</para>
            /// </summary>
            public MeshFilter? HairMesh;
            public MeshRenderer? HairRenderer;
            public MeshFilter? BeardMesh;
            public MeshRenderer? BeardRenderer;

            /// <summary>The pawn this figure is lent to, or -1 when it is parked in the pool.</summary>
            public int Pawn;

            /// <summary>
            /// Lent out to lie down as a corpse (<see cref="BorrowForCorpse"/>): nobody's live
            /// figure, and not in the pool either until it is handed back.
            /// </summary>
            public bool Borrowed;

            /// <summary>The fight as this figure is drawing it: its action, its held states, its clip layer.</summary>
            public CombatState Fight = new CombatState();

            /// <summary>
            /// The weapon prop in the right hand, seated once when the pawn's weapon changes, or
            /// null; and the item def it was made for, -1 for bare hands. See
            /// <c>PawnFigureDirector.Weapons.cs</c>.
            /// </summary>
            public GameObject? Weapon;
            public int WeaponDef = -1;

            /// <summary>
            /// The computed gait, for an animal whose row asks for one and whose rig has the
            /// four legs (design 29). Null on every colonist and on an animal that walks on its
            /// own clips.
            /// </summary>
            public QuadrupedGait? Gait;

            /// <summary>
            /// The figure's drawn box in its own frame, measured at build from the renderers'
            /// bounds; what an animal's cursor and click box are sized to. Empty on a colonist,
            /// whose cursor is the one fixed box for the whole cast.
            /// </summary>
            public Bounds DrawnBox;

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

            /// <summary>
            /// The sub-tile sidestep this figure is actually drawn with, which chases the one
            /// the pose asks for rather than taking it whole.
            ///
            /// <para>The owner's rule is that a dodge must "motion to that position or close to
            /// (be forgiving)". It is <b>the sidestep alone</b> that is eased, never the whole
            /// drawn position: rate-limiting the position damps the colonist's own walking, and
            /// a figure that cannot keep up with its own locomotion lags behind the gait its legs
            /// are playing and then surges to catch up. That surge, measured, is what a bounded
            /// 5.5 m/s adjustment on the whole position produced.</para>
            ///
            /// <para>Nearly everything the steering reads is continuous now, so this has little
            /// left to do; what it is for is the one input that genuinely cannot be — another
            /// colonist stopping or setting off, which is a step change in whether it is in the
            /// way.</para>
            /// </summary>
            public Vector3 Steer;

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
            public Transform? Chest;
            public Transform? Neck;
            public Transform? Head;
            public Transform? RightUpperArm;
            public Transform? RightLowerArm;
            public Transform? LeftUpperArm;
            public Transform? LeftLowerArm;
            public Transform? LeftHand;
            public Transform? RightHand;

            /// <summary>Runtime procedural gaze and head-look state for this figure.</summary>
            public LookGazeState Gaze;

            // The legs. Bound for the crouch; see BindWorkBones.
            public Transform? Hips;
            public Transform? LeftUpperLeg;
            public Transform? LeftLowerLeg;
            public Transform? LeftFoot;
            public Transform? RightUpperLeg;
            public Transform? RightLowerLeg;
            public Transform? RightFoot;

            /// <summary>
            /// <b>Not a hip height.</b> <c>hips.position.y - transform.position.y</c>, where the
            /// Synty humanoid avatar maps <c>HumanBodyBones.Hips</c> to a bone named <c>Root</c>
            /// sitting at the model origin — so this is nought on every one of the sixty-one
            /// characters and comes back as the 0.2 m floor <c>BindWorkBones</c> clamps it to.
            ///
            /// <para>It is left exactly as it is because the one thing that still reads it, the
            /// gesture crouch, is tuned against it by photograph: <c>ApplyGesturePose</c> draws
            /// <c>min(depth, DeepestCrouch) * StandingHipHeight</c> and the owner has signed off
            /// the stoop and the lift that come out of it. Correcting the measurement without
            /// retuning those would deepen every crouch six-fold, which is a visual change nobody
            /// asked for, and retuning them is a contact sheet rather than a test.
            ///
            /// <para><b>Do not use it as a length.</b> The sleep pose did, and laid a 2.49 m
            /// colonist down 0.38 m long (<c>docs/design/20-beds.md</c> §7b). Anything that needs
            /// a real length takes <see cref="StandingHeight"/>.</para>
            /// </summary>
            public float StandingHipHeight;

            /// <summary>
            /// The gradient of the surface this sleeper is lying on, along the way she is lying:
            /// nought on the level, positive where the foot of the bed is higher than its head.
            ///
            /// <para>Everything fixed to the grid is draped, so a bed is sheared along the ground's
            /// tangent plane while a body laid level across it sinks at one end and floats at the
            /// other. <c>docs/design/20-beds.md</c> §7b.</para>
            /// </summary>
            public float SleepSlope;

            /// <summary>
            /// This figure's drawn height, sole to crown, in metres — and so the length of body
            /// there is to lay down when it sleeps. Measured off the posed mesh at bind by
            /// <c>MeasureBody</c>; see <see cref="FigureBuild"/> for why it is not taken off a
            /// bone.
            /// </summary>
            public float StandingHeight;

            /// <summary>Thigh plus shin, in metres, measured off this figure's own rig. See
            /// <see cref="ClimbPose"/>: every foothold is a fraction of it.</summary>
            public float LegLength;

            /// <summary>The fingers, so a fist can close on a haft. See <see cref="HandGrip"/>.</summary>
            public HandGrip.Bones RightGrip;
            public HandGrip.Bones LeftGrip;

            /// <summary>
            /// The one-shot gesture being drawn, or <see cref="PawnGesture.None"/>.
            ///
            /// <para>Held on the figure rather than read from the view each frame, because the view
            /// reports the gesture as <em>sticky</em> — it goes on saying "Lift" long after the
            /// lift is over, so that no frame can miss it. What the figure is drawing is a
            /// different question from what the pawn last did.</para>
            /// </summary>
            public PawnGesture Gesture;

            /// <summary>Seconds into the gesture. Advanced in Pose, read in ApplyWorkPose.</summary>
            public float GestureClock;

            /// <summary>
            /// The serial this figure has already acted on, or -1 if it has never seen this pawn.
            ///
            /// <para>-1 rather than 0 is the whole of the first-sighting rule. A figure built for a
            /// colonist who walks into view, or every figure at all on the first frame after a
            /// load, would otherwise compare its zero against a pawn's non-zero serial and play one
            /// lift that never happened.</para>
            /// </summary>
            public int SeenSerial = -1;

            // The leg bones the footing pass needs are declared above, with the rest of the rig:
            // the crouch work bound them first and for its own reasons, and one declaration serves
            // both. Standing on uneven ground and crouching to climb want exactly the same bones,
            // which is worth noticing rather than duplicating.

            /// <summary>The lean this figure is drawn at, which eases towards the ground's own.</summary>
            public Quaternion Lean = Quaternion.identity;

            /// <summary>
            /// The height of this figure's own cell floor, with the relief taken back out.
            ///
            /// Carried because the footing pass runs later, over figures alone, with no snapshot in
            /// scope — and because by then the drawn position may have stepped in to a tree, so it
            /// can no longer be asked where the ground under this pawn is.
            /// </summary>
            public float GroundY;

            /// <summary>
            /// How far this figure's sole sits below its ankle bone, measured off its own rig.
            ///
            /// <para><b>A humanoid foot bone is the ankle, not the sole</b> — the same fact about
            /// Mecanim that had every tool in this project seated behind the hand until
            /// <c>HandGrip.Palm</c> measured where a held thing really sits. Planting the ankle on
            /// the ground therefore buries the boot by the height of the ankle above it, which at
            /// the figure's 1.4 scale is a good ten centimetres, and it reads exactly as the
            /// owner described: feet sinking into the terrain while walking.</para>
            ///
            /// <para>Measured rather than guessed, and per figure rather than once, because the
            /// cast is sixty-one characters from four packs and a boot is not the same height on
            /// all of them. Taken in the idle pose at bind time, where the figure stands at its
            /// own root and both feet are down.</para>
            /// </summary>
            public float SoleOffset;

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
            /// Whether what is being climbed is a ladder rather than a rock face.
            ///
            /// <para><b>They are not the same pose and the owner's reference photographs are why.</b>
            /// On rock the hands are spread on holds and the lower one hangs near the hip; on a
            /// ladder both hands are on rungs <i>above the head</i> and the stepped knee comes up
            /// sharply, because the rungs are a fixed ladder-width apart and the feet are on them
            /// rather than edging on whatever the stone offers.</para>
            ///
            /// <para>Latched beside <see cref="LastClimbFace"/> and for the same reason: it has to
            /// outlive the face while the weight eases back out, or the pose changes shape halfway
            /// through stepping off the top.</para>
            /// </summary>
            public bool OnLadder;

            /// <summary>
            /// How much of a swimmer this figure is, 0 on dry land and 1 afloat.
            ///
            /// <para>Eased rather than switched, like <see cref="ClimbWeight"/>: the drawn height
            /// rises by nearly two metres between the bank and the water, and a pose that snapped
            /// on at the water's edge would be a colonist changing shape in one frame. The target
            /// comes from <c>WaterLine.Weight</c>, which blends over the step, so the pose and the
            /// height come on together — a figure lying prone while still standing on the bank is
            /// the same fault as one floating on dry ground.</para>
            /// </summary>
            public float SwimWeight;

            /// <summary>
            /// The swim stroke's own clock, in seconds of game time.
            ///
            /// <para>A clock and not a phase taken from the step, which is the opposite choice to
            /// <see cref="ClimbPhase"/> and is deliberate. A climb's reach has to match the height
            /// gained, so it is driven by the simulation's own progress; a swim stroke matches
            /// nothing in particular and simply runs, so it is a stroke in the
            /// <c>WorkStroke</c> sense. It advances on game time, so a paused colony stops
            /// swimming, which is the rule the whole director already follows.</para>
            /// </summary>
            public float SwimClock;

            /// <summary>
            /// How much of a sleeper this figure is, 0 standing and 1 flat out. Eased, so a
            /// colonist lies down and gets up rather than snapping between the two.
            /// </summary>
            public float SleepWeight;

            /// <summary>
            /// Which way the body lies, head to foot. The bed's own facing where there is a bed,
            /// and whatever the colonist was facing when it dropped where there is not.
            /// </summary>
            public Vector3 SleepAlong = Vector3.forward;

            /// <summary>
            /// Where the head goes in plan: the middle of the pillow, or the cell's own centre
            /// stepped back half a body where there is no bed. The head rather than the middle,
            /// because the head is the end that has to land somewhere exact.
            /// </summary>
            public Vector3 SleepHeadAt;

            /// <summary>World height of the surface being lain on — a mattress top, or the ground.</summary>
            public float SleepSurfaceY;

            /// <summary>
            /// Degrees this figure is aiming its stroke below level, this frame.
            ///
            /// The style's <see cref="WorkStyle.Dip"/> when the work is a layer down and zero when
            /// it is not, so the same mining style covers an adit cut level into a face and a shaft
            /// cut down from the rim without needing two of them.
            /// </summary>
            public float WorkDip;

            /// <summary>
            /// The item def index this colonist has in its arms, or -1 for empty-handed. Design 24.
            ///
            /// <para>Carried on the figure for the same reason <see cref="WorkJob"/> is: the pose
            /// pass runs later, over figures alone, with no snapshot in scope.</para>
            /// </summary>
            public int CarryDef = -1;

            /// <summary>How many are in the load. The arms ignore it; the inspect pane does not.</summary>
            public int CarryStack;

            /// <summary>
            /// How much of a carrier this figure is, 0 empty-handed and 1 in the full scoop. Eased
            /// by <see cref="CarryPose.Settle"/>, so the arms fold into the cradle as the lift's
            /// crouch releases them rather than snapping there on one frame.
            /// </summary>
            public float CarryWeight;

            /// <summary>
            /// Where the load was drawn on the last posed frame, and whether there was one.
            ///
            /// <para>Recorded rather than recomputed because the renderer needs it <em>after</em>
            /// the pose pass has run — the cradle is measured off palms that do not exist until
            /// the arms have been posed. Recomputing it on the renderer's side would mean reading
            /// bones a frame late, which is the drift that would show up as a load lagging behind
            /// the hands that hold it.</para>
            /// </summary>
            public Vector3 CarryAt;

            /// <summary>Whether <see cref="CarryAt"/> was written this frame. See it for why.</summary>
            public bool CarryPlaced;

            /// <summary>
            /// Which way the load is turned: the figure's own yaw at the moment it was placed.
            ///
            /// <para><b>The load turns with the colonist</b> (owner, 2026-09-19: "when you turn a
            /// direction the logs don't turn with you and they should"). Taken from the figure
            /// rather than from <c>FacingOf</c>, which is the renderer's memory of the last
            /// heading it drew a colonist at — and it never writes an entry for a pawn that has a
            /// live figure, because that loop skips them. Every load on a real colonist was
            /// therefore drawn at a yaw of exactly nought.</para>
            /// </summary>
            public float CarryYaw;

            /// <summary>
            /// Where the load is easing from, while it is still arriving in or leaving the hands,
            /// and how far through that it is. See <see cref="CarryHandover"/>.
            /// </summary>
            public Vector3 HandoverFrom;

            /// <summary>Seconds into the current hand-over, or past its end when there is none.</summary>
            public float HandoverClock = float.MaxValue;

            /// <summary>
            /// The thing this figure last let go of, and where its hands were when it did.
            ///
            /// <para>Kept after the load has left, because the load is then an item lying in a
            /// cell drawn by the renderer, and the renderer is the only thing that knows where
            /// that cell is. The figure supplies the other end of the fall.</para>
            /// </summary>
            public int ReleasedThing = -1;

            /// <summary>Where the hands were at the release. See <see cref="ReleasedThing"/>.</summary>
            public Vector3 ReleasedFrom;

            /// <summary>Seconds since the release, or past the settle's end when there is none.</summary>
            public float ReleasedClock = float.MaxValue;

            /// <summary>The item the figure is holding, so a release knows what was let go of.</summary>
            public int CarryThing = -1;

            // ---- the arms as the rig authored them, for the carry's stiffness. See BindWorkBones.
            public Quaternion RestRightUpperArm = Quaternion.identity;
            public Quaternion RestRightLowerArm = Quaternion.identity;
            public Quaternion RestLeftUpperArm = Quaternion.identity;
            public Quaternion RestLeftLowerArm = Quaternion.identity;
            public bool RestArmsBound;

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

            /// <summary>The butt of the haft, in the tool's own space. The end away from the head.</summary>
            public Vector3 Butt;

            /// <summary>Up the haft from the butt, in the tool's own space, a unit vector.</summary>
            public Vector3 Haft;

            /// <summary>How long the haft is, in metres. See <see cref="Butt"/>.</summary>
            public float HaftLength;

            /// <summary>
            /// How the tool sits in the hand, as a rotation in the <em>hand's</em> own frame.
            ///
            /// <para>A tool in a fist is a rigid attachment, and this is what says so. Everything
            /// the fitting worked out — the haft along the forearm, the bit turned the way the head
            /// travels, the roll and the yaw — is settled once against the mesh and is thereafter a
            /// fact about how this prop lies in this hand. Stored local rather than world because
            /// the hand moves constantly and the grip does not.</para>
            /// </summary>
            public Quaternion Seat = Quaternion.identity;

            /// <summary>Whether <see cref="Seat"/> has been measured yet. Nothing places a tool
            /// that has not been fitted.</summary>
            public bool Seated;

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
