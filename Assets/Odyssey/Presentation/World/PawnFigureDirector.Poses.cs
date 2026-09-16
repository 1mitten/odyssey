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
    /// <see cref="PawnFigureDirector"/>: the poses laid over the gait.
    ///
    /// <para>Footing, the work stroke, the climb and the gestures — everything that writes a bone
    /// angle or a foot position on top of whatever the animation mixer produced. Split out of the
    /// director on 2026-09-16 because the one file had reached 2,787 lines; it is the same class
    /// and the same behaviour, and a member here may call one in any other part.</para>
    ///
    /// <para>The governing rule is <c>13-gestures.md</c> §3: <b>author the angles when the figure
    /// aims at something whose position we do not know; solve to a point when it must meet
    /// something whose position we do.</b></para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
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
        /// <summary>
        /// Put both feet on the ground that is actually drawn, and bend the legs to suit.
        ///
        /// <para><b>Why it runs before the work pose and not after.</b> The hips are a parent of
        /// the spine, so dropping them after the arms have been posed translates the arms with
        /// them — and the left hand is solved onto a world point on the axe haft, so it would come
        /// away from the haft by exactly the drop. Footing first, then the swing over the top of
        /// it, and the arms are solved against hips that have stopped moving.</para>
        ///
        /// <para><b>Why it is idempotent.</b> Like <see cref="ApplyWorkPose"/>, this runs at the
        /// end of both <see cref="Sync"/> and <see cref="Evaluate"/> because which of those is the
        /// last word depends on whether there is a player loop. Running twice is harmless: the
        /// targets are taken from the foot positions before anything moves, the feet are then
        /// solved onto them, and a second pass finds the feet already on the ground, corrects by
        /// nothing and returns before it touches the hips.</para>
        ///
        /// <para><b>What it does not do.</b> It never reads the simulation and never changes it.
        /// A colonist on a slope walks at exactly the speed one on the flat does, because slope is
        /// not a property of a cell — this is the picture of the ground, not a fact about it.</para>
        /// </summary>
        void ApplyFooting()
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Pawn < 0) continue;

                // A rig with no legs bound is not an error: a non-Humanoid prefab answers null to
                // every bone and simply goes on walking, which is what it does for the arms too.
                if (figure.LeftFoot == null || figure.RightFoot == null) continue;
                if (figure.LeftUpperLeg == null || figure.RightUpperLeg == null) continue;

                Vector3 leftAt = figure.LeftFoot.position;
                Vector3 rightAt = figure.RightFoot.position;

                // The ground under each foot separately, not under the figure. That is the whole
                // point: on a slope the two are at different heights, and asking once at the
                // body's own position would move both feet by the same amount and leave the
                // figure standing on one heel exactly as before.
                float leftGround = figure.GroundY + GroundRelief.HeightAt(leftAt.x, leftAt.z);
                float rightGround = figure.GroundY + GroundRelief.HeightAt(rightAt.x, rightAt.z);

                float left = Footing.Correction(leftAt.y, leftGround);
                float right = Footing.Correction(rightAt.y, rightGround);
                if (left == 0f && right == 0f) continue;

                Vector3 leftTarget = leftAt + Vector3.up * left;
                Vector3 rightTarget = rightAt + Vector3.up * right;

                // The hips drop before the legs are solved, so each leg is solved against where
                // the body has actually ended up. Doing it the other way round solves both legs
                // and then moves them, which is the same as not having done it.
                float drop = Footing.HipDrop(left, right);
                if (drop != 0f && figure.Hips != null)
                    figure.Hips.position += Vector3.up * drop;

                Transform body = figure.Transform;
                PlantFoot(figure.LeftUpperLeg, figure.LeftLowerLeg, figure.LeftFoot, leftTarget, body, figure.Lean);
                PlantFoot(figure.RightUpperLeg, figure.RightLowerLeg, figure.RightFoot, rightTarget, body, figure.Lean);
            }
        }

        /// <summary>
        /// Bend one leg so its foot lands on a point, and lay the foot along the ground there.
        ///
        /// <para>The solve is <see cref="TwoBoneIk.Reach"/> unchanged — it is a two-bone analytic
        /// solve written against transforms and its own header says it is not about arms. A leg is
        /// two bones and a target, which is exactly what it takes.</para>
        ///
        /// <para>The pole is ahead of the knee and below it, because a knee bends forward. Sent
        /// the other way the solve is equally correct and the leg bends backwards, which is a
        /// perfectly valid pose for a bird.</para>
        /// </summary>
        static void PlantFoot(Transform? upper, Transform? lower, Transform? foot, Vector3 target,
            Transform body, Quaternion lean)
        {
            if (upper == null || lower == null || foot == null) return;

            Vector3 pole = upper.position + body.forward * 1.2f - body.up * 0.4f;
            TwoBoneIk.Reach(upper, lower, foot, target, pole);

            GroundRelief.SlopeAt(target.x, target.z, out float slopeX, out float slopeZ);
            foot.rotation = Footing.AnkleLevel(Footing.GroundNormal(slopeX, slopeZ), lean) * foot.rotation;
        }

        void ApplyWorkPose()
        {
            // Cleared every pass, because the crouch's own early return — nothing to do at the top
            // of a motion — would otherwise leave the last non-zero reading standing. The first
            // contact sheet reported 0.22 m of crouch on a figure that was plainly upright, which
            // is worse than reporting nothing: a measurement exists to be trusted over the picture,
            // and one that lies is the only thing on the board with no way of being caught.
            MeasuredCrouchDrop = 0f;
            CrouchedFigures = 0;
            LeglessFigures = 0;
            MeasuredFootReach = 0f;
            MeasuredToolDrift = 0f;

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
                    else if (figure.Gesture != PawnGesture.None || ForceGesture.HasValue)
                        ApplyGesturePose(figure);
                    continue;
                }

                if (figure.RightUpperArm == null) continue;

                WorkStyle look = Styles[figure.Style];

                // One phase, read once. The angles and the slide are two views of the same instant
                // and deriving them separately is an invitation for them to disagree on the frame
                // the blow lands, which is the one frame anybody is looking at.
                float strokePhase =
                    HeldPhase ?? look.Stroke.Phase(figure.SwingClock, figure.SwingOffset);
                WorkSwing swing = look.Stroke.At(strokePhase);

                // Where the working hand has slid to. Eased with the rest of the pose by the same
                // weight, so a colonist takes the tool up the haft as it raises it rather than the
                // axe jumping through its fist on the frame the work starts.
                float gripAt = Mathf.Lerp(
                    look.GripFraction,
                    look.Stroke.GripAt(strokePhase, look.SlideFraction, look.GripFraction),
                    figure.WorkWeight);

                // Dipped before scaled, so the lean comes on with the rest of the pose rather than
                // snapping into a bow the frame the work starts.
                Strike(figure, swing.Dipped(figure.WorkDip).Scaled(figure.WorkWeight), look.Tilt, gripAt);

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

                // Out of the cut, which is back towards whoever swung: an edge biting across the
                // grain throws wood at the woodcutter, not away into the forest.
                Vector3 outward = figure.Transform.position - figure.WorkCentre;
                outward.y = 0f;

                // Where the blow lands: the blade's own tip when there is a blade, and the thing
                // being struck when there is not.
                //
                // **A clone without the art packs fells trees bare-handed**, and an axe that is
                // not in anyone's hands must not be what decides whether the work can be heard.
                // This guard used to sit above everything here and take the sound with it, so a
                // checkout with no Synty made no chopping noise at all, at any zoom — which is
                // exactly the bargain the chips already refuse to make.
                Transform? blade = figure.Held.Transform;
                Vector3 edge = BlowPoint(
                    blade == null ? null : blade.TransformPoint(figure.Held.BladeTip),
                    figure.WorkCentre, outward, look.ChipStandOff);

                // Which debris, chosen from the job the snapshot already publishes. This is the
                // smallest possible version of what docs/design/12-work-poses-and-tools.md calls
                // a WorkStyle — that note bundles the tool, the stroke, the grip and the chips
                // into one value selected from PawnView.JobDef, and the whole point of its design
                // is that the contract needs no change because JobDef is published already. Only
                // the chips are switched here; a pick in the hands and a stroke of its own are
                // that piece of work, not this one.
                if (blade != null) Chips?.Throw(look.Chips, edge, outward);

                // And the sound of the blow, on the same frame and from the same edge the chips
                // leave. See <see cref="BlowLanded"/>.
                BlowLanded?.Invoke(figure.Style, edge);
            }
        }

        /// <summary>
        /// Where a blow lands, given the blade's tip if there is one.
        ///
        /// <para>Stood off from the face rather than left wherever the head stopped: the head
        /// finishes <i>inside</i> the thing it struck, which for a 2.5 m block of stone means
        /// debris born inside solid rock and a sound coming from within it.</para>
        ///
        /// <para>Static and public because it is the whole of the decision and none of the
        /// scene: a test can ask what happens with no blade without building a figure, a rig or
        /// a character to hang one on.</para>
        /// </summary>
        public static Vector3 BlowPoint(Vector3? bladeTip, Vector3 workCentre, Vector3 outward,
            float standOff)
        {
            Vector3 point = bladeTip ?? workCentre;
            if (standOff > 0f && outward.sqrMagnitude > 1e-6f)
                point += outward.normalized * standOff;
            return point;
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
            // Two reaches a cell. One would have a colonist take a whole three metres in a single
            // grab, which reads as being hauled up rather than as climbing.
            const float ReachesPerCell = 2f;

            // Overhead and down at the hip. Not as far back as a pick's raise: a climber's hand
            // goes up the wall in front of it, not over its own crown.
            const float Reaching = -150f;
            const float Pulling = -35f;
            const float ElbowBend = -30f;

            float swing = Mathf.Sin(figure.ClimbPhase * ReachesPerCell * 2f * Mathf.PI);

            if (figure.RightUpperArm != null && figure.LeftUpperArm != null)
            {
                float right = Mathf.Lerp(Pulling, Reaching, (swing + 1f) * 0.5f) * figure.ClimbWeight;
                float left = Mathf.Lerp(Reaching, Pulling, (swing + 1f) * 0.5f) * figure.ClimbWeight;

                // No tilt: a climb is straight up the sagittal plane, where a swing is across the
                // body.
                Vector3 axis = SwingAxis(figure.Transform, 0f);

                Pitch(figure.RightUpperArm, axis, right);
                Pitch(figure.RightLowerArm, axis, ElbowBend * figure.ClimbWeight);
                Pitch(figure.LeftUpperArm, axis, left);
                Pitch(figure.LeftLowerArm, axis, ElbowBend * figure.ClimbWeight);
            }

            ApplyClimbLegs(figure, swing);
        }

        /// <summary>
        /// And the boots on the rock, which is the half of a climb this pose did without until the
        /// legs were bound.
        ///
        /// <para>The arms alone read as climbing at this camera height — that was the bargain the
        /// pose was written under and it was an honest one — but underneath them the gait mixer was
        /// still playing the idle, because a purely vertical step has no ground speed. So a
        /// colonist went up a shaft hauling on the wall with its boots together, standing to
        /// attention. Legs make it a climb.</para>
        ///
        /// <para><b>Contralateral, solved, and eased in world space.</b> The first is
        /// <see cref="ClimbPose.StepsFrom"/>'s business and the second
        /// <see cref="ClimbPose.Foothold"/>'s. The third is here: at zero weight the target is
        /// exactly where the animation put the boot, so stepping on to a wall and off it again is
        /// continuous by construction rather than by a number that has to be kept in step with the
        /// arms' own ease.</para>
        ///
        /// <para><b>The sole is left as the gait wrote it</b>, restored after the solve the same
        /// way <see cref="SolveLeg"/> restores it. A foot flat against a vertical face wants its
        /// toes pointing up, and which rotation that is depends on the rig's own convention for a
        /// foot bone — a thing settled by photographing a figure, which no worktree can do. What
        /// the gait leaves is toes forward, and the figure is turned to face the wall, so the boots
        /// address the rock toes-first: a climber edging on small holds, which is a real way to
        /// stand on rock rather than a placeholder pretending to be one.</para>
        /// </summary>
        void ApplyClimbLegs(Figure figure, float swing)
        {
            if (figure.LegLength <= 0f) return;

            ClimbPose.StepsFrom(swing, out float left, out float right);

            Vector3 toRock = figure.LastClimbFace;
            if (toRock == Vector3.zero) toRock = figure.Transform.forward;

            // How far in front of the figure the stone actually is, which is the one number that
            // puts both boots on one plane rather than on a cone. The face of the cell is half a
            // cell from its centre and the lean has already carried the body most of the way to it,
            // so what is left is the gap the legs have to cross — and it is ClimbLean's own
            // arithmetic rather than a second constant that would drift out of step with it.
            toRock = toRock.normalized * Mathf.Max(0.05f, CellMetrics.HalfXZ - ClimbLean);

            PlantFoot(figure, figure.LeftUpperLeg, figure.LeftLowerLeg, figure.LeftFoot, toRock, left);
            PlantFoot(figure, figure.RightUpperLeg, figure.RightLowerLeg, figure.RightFoot, toRock, right);
        }

        /// <summary>One boot on to its hold, eased out of wherever the gait had it.</summary>
        void PlantFoot(Figure figure, Transform? upper, Transform? lower, Transform? foot,
            Vector3 toRock, float step)
        {
            if (upper == null || lower == null || foot == null) return;

            Vector3 hold = ClimbPose.Foothold(upper.position, toRock, figure.Transform.up,
                figure.LegLength, step);
            Vector3 target = Vector3.Lerp(foot.position, hold, Mathf.Clamp01(figure.ClimbWeight));

            Quaternion sole = foot.rotation;

            // The knee goes towards the rock, which is where a climber's knee goes and is also
            // simply where a knee goes: it bends forwards. A leg solved with an arm's pole bends
            // backwards, which does not read as a bad pose, it reads as a broken person.
            TwoBoneIk.Reach(upper, lower, foot, target, lower.position + toRock.normalized);
            foot.rotation = sole;

            // How far the boot finished from the hold it was sent to. The climb's own
            // MeasuredBladeGap: a leg that has run out of reach straightens towards its target and
            // stops, which in a photograph is indistinguishable from a leg that arrived.
            float missed = Vector3.Distance(foot.position, target);
            if (missed > MeasuredFootReach) MeasuredFootReach = missed;
        }

        /// <summary>Which curve a gesture follows. See <see cref="Gesture"/>.</summary>
        static Gesture GestureOf(PawnGesture kind) =>
            kind == PawnGesture.Stow ? Gesture.Stow : Gesture.Lift;

        /// <summary>
        /// How far down a crouch may take the hips before the legs are asked for more than they
        /// have. A fraction of the figure's own standing hip height.
        ///
        /// <para>Past about half, a two-bone solve with the foot pinned runs out of leg: the knee
        /// reaches full flexion and the solver straightens towards an unreachable target, which
        /// draws as a figure kneeling through its own shins. <see cref="Gesture.Lift"/> asks for a
        /// third, so this is a guard rail for whatever asks for more later and not a number
        /// anything currently touches.</para>
        /// </summary>
        public const float DeepestCrouch = 0.45f;

        /// <summary>
        /// How far out from the shoulder the off-hand elbow is sent, in metres. Keeps it clear of
        /// the ribs — a left arm reaching across the body for a haft held in the right hand folds
        /// its elbow straight through the torso if the pole is anywhere near the midline.
        /// </summary>
        public const float OffHandElbowOut = 1.0f;

        /// <summary>
        /// How far *above* the shoulder the off-hand elbow is sent, in metres, and therefore which
        /// of the two arms passes over the other.
        ///
        /// <para>Positive lifts the off elbow so the forearm crosses above the working arm.
        /// Negative drops it underneath, which is what this was — the two arms reach the same way
        /// along one haft, so with a low elbow they lie in the same place and their meshes
        /// intersect at the wrists.</para>
        ///
        /// <para>Modest on purpose: an elbow much above the shoulder is a chicken wing, and reads
        /// as a person struggling with something heavy rather than gripping it.</para>
        /// </summary>
        public const float OffHandElbowLift = 0.35f;

        /// <summary>
        /// Stoop to the ground and straighten up again: the lift, the stow, and whatever else ends
        /// up reaching the floor.
        ///
        /// <para><b>A crouch is two angles and one translation, and the translation is what keeps
        /// the boots on the ground.</b> Bend a knee by rotating the leg and the foot swings up off
        /// the floor — the figure treads air with its pelvis exactly where it always was, which
        /// reads as sitting on an invisible stool. So the pelvis comes down by the crouch depth
        /// first, and then each leg is solved back to the foot the gait had already put down.</para>
        ///
        /// <para><b>Why the legs are solved and not authored.</b> Every angle in
        /// <see cref="WorkSwing"/> had to be settled against a photograph, because a stroke aims at
        /// something whose position the arithmetic does not know. A crouch is the opposite case: we
        /// know exactly where the feet are, because we just read them. Authored knee angles would
        /// also have to be authored sixty-one times — the characters differ in proportion and the
        /// director scales them besides — where one solve is right on all of them.
        /// <c>13-gestures.md</c> §3 and §4 are the argument.</para>
        ///
        /// <para><b>The feet are not solved against the terrain</b>, which is the owner's decision
        /// (2026-09-16) and not an omission. They are solved against where the walk cycle put them
        /// this frame, so on a slope they are as right or as wrong as the walk already was — and
        /// the crouch adds no error of its own.</para>
        ///
        /// <para>The hands reach for the ground in front of the boots rather than for the item's
        /// own drawn position. The item is in the pawn's own cell by construction — the haul's
        /// pickup toil fails unless it is — so its cell centre and the figure's feet are the same
        /// place, and asking <c>ItemHeap</c> where it drew the pile would couple the pose to the
        /// pile's own scatter for a difference of a few centimetres. Hands stay empty besides
        /// (owner, 2026-09-16), so there is nothing there to meet.</para>
        /// </summary>
        void ApplyGesturePose(Figure figure)
        {
            if (figure.Hips == null || figure.StandingHipHeight <= 0f)
            {
                LeglessFigures++;
                return;
            }

            CrouchedFigures++;

            Gesture motion = GestureOf(ForceGesture ?? figure.Gesture);
            float phase = HeldGesturePhase ?? motion.Phase(figure.GestureClock);
            float depth = motion.At(phase) * motion.Depth;
            if (depth <= 1e-4f) return;

            depth = Mathf.Min(depth, DeepestCrouch) * figure.StandingHipHeight;

            // The deepest of the figures drawn this pass, not the last one, so that a colony in
            // which one colonist is stooping reports that colonist rather than whoever came last
            // in the list and was standing.
            if (depth > MeasuredCrouchDrop) MeasuredCrouchDrop = depth;

            // Read the feet BEFORE the pelvis moves. After it, they have already been dragged
            // down through the skeleton and the solve would be asked to put them back where the
            // crouch has just carried them, which is an elaborate way of doing nothing.
            Vector3 leftFoot = figure.LeftFoot != null ? figure.LeftFoot.position : Vector3.zero;
            Vector3 rightFoot = figure.RightFoot != null ? figure.RightFoot.position : Vector3.zero;
            Quaternion leftSole = figure.LeftFoot != null ? figure.LeftFoot.rotation : Quaternion.identity;
            Quaternion rightSole = figure.RightFoot != null ? figure.RightFoot.rotation : Quaternion.identity;

            // Down, and a little back: a person lowering their weight puts their hips behind their
            // heels, or they fall forward over their own toes. Small, because the figure is drawn
            // from a long way up and a big shift reads as sitting down.
            Vector3 back = -figure.Transform.forward * (depth * 0.25f);

            // Added to wherever the animation put the pelvis, exactly as Pitch is added to whatever
            // rotation the animation gave a bone, and it rests on the same thing: that the graph
            // rewrites the skeleton between the two places this pass runs. It does — if it did not,
            // the axe swing would have been drawing at twice its angles since the day it landed.
            // A translation is the more alarming of the two to get wrong, because a doubled drop
            // puts the colonist's knees through the floor rather than merely overacting.
            figure.Hips.position += Vector3.down * depth + back;

            Vector3 knee = figure.Transform.forward;
            SolveLeg(figure.LeftUpperLeg, figure.LeftLowerLeg, figure.LeftFoot, leftFoot, leftSole, knee);
            SolveLeg(figure.RightUpperLeg, figure.RightLowerLeg, figure.RightFoot, rightFoot, rightSole, knee);

            // And the rest of the body follows the hips down to the floor. The back folds over the
            // work — a person picking something up does not keep a parade-ground spine — and both
            // arms hang towards it rather than being solved at a target, because with nothing in
            // the hands there is no point in space they have to meet. See 13-gestures.md §1: the
            // owner took the empty-handed version knowingly, and the reach is what becomes an IK
            // solve on the day a crate appears in the fists.
            float reach = motion.Hands(phase);
            Vector3 axis = SwingAxis(figure.Transform, 0f);
            Pitch(figure.Spine, axis, 42f * reach);
            Pitch(figure.RightUpperArm, axis, -34f * reach);
            Pitch(figure.LeftUpperArm, axis, -34f * reach);
            Pitch(figure.RightLowerArm, axis, -18f * reach);
            Pitch(figure.LeftLowerArm, axis, -18f * reach);
        }

        /// <summary>
        /// Put one foot back where it was, by bending the leg above it.
        ///
        /// <para>The pole goes in front of the knee, which is the one thing that differs from an
        /// arm: an elbow bends backwards and a knee bends forwards, and a leg solved with an arm's
        /// pole bends the wrong way — which does not read as a bad pose, it reads as a broken
        /// person.</para>
        ///
        /// <para>The sole's own rotation is restored afterwards. Without that the foot inherits
        /// whatever the shin ended up doing and the toes point into the floor, which is the visible
        /// half of the same mistake.</para>
        /// </summary>
        static void SolveLeg(Transform? upper, Transform? lower, Transform? foot,
            Vector3 target, Quaternion sole, Vector3 forward)
        {
            if (upper == null || lower == null || foot == null) return;
            if (target == Vector3.zero) return;

            TwoBoneIk.Reach(upper, lower, foot, target, lower.position + forward);
            foot.rotation = sole;
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
    }
}
