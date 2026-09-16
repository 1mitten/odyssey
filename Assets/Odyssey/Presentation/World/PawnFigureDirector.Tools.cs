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
    /// <see cref="PawnFigureDirector"/>: putting a tool in a figure's hands, and measuring the
    /// stroke it makes.
    ///
    /// <para>Fitting, gripping and re-gripping the axe, pick and hammer, and the measurement
    /// passes that <c>SwingCheck</c> reads. Split out of the director on 2026-09-16 because the
    /// one file had reached 3,033 lines; it is the same class and the same behaviour.</para>
    ///
    /// <para>A tool is <b>gripped by measurement, not by authored Euler angles</b>: the haft is
    /// the long axis of the combined mesh bounds, the head is the end the mass sits towards, and
    /// the blade's roll is computed. And <b>a humanoid hand bone is the wrist</b>, so where a held
    /// thing really sits is measured off the knuckles by <c>HandGrip.Palm</c>.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>
        /// Put one figure into one moment of the stroke.
        ///
        /// Separate from the loop because the reach measurement needs exactly this and nothing
        /// else: strike the pose, look at where the edge ended up.
        /// </summary>
        /// <summary>
        /// Strike the pose and leave every tool where it is.
        ///
        /// <para><b>What the measuring paths want, and the distinction is not cosmetic.</b> They
        /// pose a figure in order to fit a prop or to read where its edge lands, and they do it for
        /// <em>every</em> style in turn without setting <see cref="Figure.Style"/> — so anything
        /// here that reached for "the tool in the hands" would take hold of whichever prop happened
        /// to be selected and move it while measuring a different one. Nothing needs to slide in
        /// those paths anyway: they all strike <c>AtStrike</c>, where the working hand is at
        /// <see cref="WorkStyle.GripFraction"/>, which is exactly where <c>GripTool</c> seated
        /// it.</para>
        /// </summary>
        void Strike(Figure figure, WorkSwing swing, float tilt) =>
            Strike(figure, swing, tilt, null);

        /// <summary>
        /// Put one figure into one moment of the stroke, with its working hand a given fraction of
        /// the way up the haft.
        ///
        /// <paramref name="gripAt"/> is what makes the hands slide, and null is what says not to —
        /// see the overload above.
        /// </summary>
        void Strike(Figure figure, WorkSwing swing, float tilt, float? gripAt)
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

            TakeHold(figure, axis, swing, gripAt);
        }

        /// <summary>
        /// Put the tool in the working fist: rigid in the hand, and a given fraction up the haft.
        ///
        /// <para><b>An axe must never spin, and one was</b> (owner, 2026-09-16). The cause was not
        /// in the fitting or in the swing but in how the tool was kept still while the wrist turned.
        /// The old code captured the tool's <em>world</em> pose, rotated the hand, and put the world
        /// pose back. On a child object that last step writes a <em>local</em> rotation worked out
        /// from the parent's rotation at that instant — so the tool's local transform was being
        /// integrated, one small correction at a time, rather than recomputed. It had nothing to
        /// converge to, so it wound.</para>
        ///
        /// <para><b>It wound twice per frame, and only in the game.</b> <c>ApplyWorkPose</c> runs
        /// at the end of both <c>Sync</c> and <c>Evaluate</c>, and only <c>Evaluate</c> re-evaluates
        /// the animation graph first — so one of the two passes starts from bones the previous frame
        /// already posed. For a <see cref="Pitch"/> that is harmless, because the second pass
        /// re-derives the angle from a clean skeleton and the first pass's result is discarded
        /// unseen. For anything that accumulates it is fatal. And a contact sheet could not have
        /// caught it: the harnesses step the graph by hand, one pose per picture.</para>
        ///
        /// <para>So the tool is <em>placed</em>, never adjusted: its rotation is reset to the seat
        /// the fitting measured, and it is then slid along its own haft until the grip point is in
        /// the palm. Both are absolute, so running it twice does nothing the second time and a
        /// dropped frame leaves no trace. That is the same property <see cref="ClimbPose"/> and the
        /// crouch rely on, and the rule is worth stating once for all of them: <b>a pose may add to
        /// a bone, because the graph rewrites bones; it may never add to anything the graph does not
        /// own.</b> The animation graph has never heard of a prop.</para>
        ///
        /// <paramref name="gripAt"/> is where the working hand is on the haft, which is what makes
        /// the hands slide; null takes the grip the blow is struck with.
        /// </summary>
        void PlaceTool(Figure figure, float? gripAt)
        {
            // The held one, deliberately and only here: this runs on the drawing path, where the
            // style in the hands is the style being posed. See the no-slide overload of Strike for
            // why that is not true of the measuring paths.
            FittedTool fitted = figure.Held;
            Transform? tool = fitted.Transform;
            if (tool == null || !fitted.Seated || fitted.HaftLength <= 0f) return;

            // How far it had wandered since it was last put right. Zero every frame is what "rigid
            // in the fist" means, and it is worth measuring rather than assuming: this is exactly
            // the fault that drew as a plausible grip on a tumbling axe, and the number is the only
            // thing that would notice it coming back.
            float drift = Quaternion.Angle(tool.localRotation, fitted.Seat);
            if (drift > MeasuredToolDrift) MeasuredToolDrift = drift;

            tool.localRotation = fitted.Seat;

            float at = Mathf.Clamp01(gripAt ?? Styles[figure.Style].GripFraction);
            MeasuredGripAt = at;
            Vector3 grip = fitted.Butt + fitted.Haft * (fitted.HaftLength * at);
            tool.position += HandGrip.Palm(figure.RightGrip) - tool.TransformPoint(grip);
        }

        /// <summary>
        /// Put both fists on the haft of whatever this figure is working with.
        ///
        /// <para>All of the hard part is in <see cref="Grasp"/> now, and that is the point: taking
        /// hold of something arrived here as part of an axe swing, and a ladder rung, a rifle
        /// fore-end, a carried crate and the other end of a stretcher all want the same thing and
        /// none of them is a swing. What is left here is the part that really is about this figure
        /// and this tool — where the haft is, how far apart the hands go on it, and the fact that
        /// the working hand has an axe hanging off it.</para>
        ///
        /// <para><b>The tool is put back where it was.</b> Turning a wrist turns everything
        /// parented to it, and where that blade points was settled against photographs — the roll,
        /// the yaw, and the measured strike offset the whole stance is solved from. So the tool's
        /// place in the world is taken before the hand moves and restored after, and the fist
        /// rotates inside a stationary axe.</para>
        ///
        /// <para>A figure with no tool holds nothing rather than clenching: a clone without the
        /// packs has colonists chopping bare-handed, and bare hands balled into fists would be a
        /// worse picture than open ones.</para>
        /// </summary>
        void TakeHold(Figure figure, Vector3 axis, WorkSwing swing, float? gripAt)
        {
            Transform? tool = figure.Held.Transform;
            if (tool == null)
            {
                // No tool: the off arm swings in sympathy, which is what it did before there was
                // ever anything to hold and is still the right answer for empty hands.
                Pitch(figure.LeftUpperArm, axis, swing.Shoulder - swing.Spine);
                Pitch(figure.LeftLowerArm, axis, swing.Elbow);
                return;
            }

            float amount = Mathf.Clamp01(figure.WorkWeight);
            Transform body = figure.Transform;

            // **The working wrist is not turned to the haft, and that is not an omission.** It was
            // turned, briefly, and the axe came out facing the wrong way (owner, 2026-09-16) — of
            // course it did: the tool hangs off this hand, so any roll given to the wrist is a roll
            // given to the blade, and where the blade points was settled against photographs. The
            // old code turned the wrist and then undid the damage by pinning the tool's world pose,
            // which is the integration that made it spin. Both halves of that were wrong. This hand
            // holds the tool the way the fitting laid it; the stroke's own angles say where it
            // points; and the only thing the grip does here is close the fingers.
            //
            // Fingers first, because curling them turns finger bones and not the hand, so the tool
            // does not move — and PlaceTool wants the palm already in the shape it will be gripping
            // in when it seats the haft into it.
            HandGrip.Close(figure.RightGrip, amount);

            PlaceTool(figure, gripAt);

            // Only now is the haft somewhere. Read after placing, or the off hand is sent to where
            // the wood was a frame ago — which at the top of a raise is a good half metre out.
            Vector3 butt = tool.TransformPoint(figure.Held.OffHandGrip);
            Vector3 head = tool.TransformPoint(figure.Held.BladeTip);
            Hold haft = Hold.Bar(butt, head);

            // Out to the side and up, so the off elbow leaves the ribs and the off forearm passes
            // over the working one rather than through it.
            var offArm = new GripArm(figure.LeftUpperArm, figure.LeftLowerArm, figure.LeftGrip,
                new Vector3(-OffHandElbowOut, OffHandElbowLift, 0f));

            MeasuredGripGap = Grasp.One(offArm, haft, 0f, body.right, body.up, amount);

            // Can the arm even get there? TwoBoneIk straightens towards a target it cannot reach
            // and stops, which is the right behaviour and is indistinguishable, in a photograph or
            // in a distance-to-the-haft measurement, from a solve that simply missed. Positive here
            // means the haft is further from the shoulder than the arm is long, and no solver will
            // ever close that gap — the working hand's slide up the haft is what brings it near.
            if (figure.LeftUpperArm != null && figure.LeftLowerArm != null && figure.LeftHand != null)
            {
                float armLength =
                    Vector3.Distance(figure.LeftUpperArm.position, figure.LeftLowerArm.position)
                    + Vector3.Distance(figure.LeftLowerArm.position, figure.LeftHand.position);
                MeasuredGripSpan = Vector3.Distance(figure.LeftUpperArm.position, butt);
                MeasuredGripLocal = Quaternion.Inverse(body.rotation) * (butt - body.position);
                MeasuredPalmLocal = Quaternion.Inverse(body.rotation)
                    * (HandGrip.Palm(figure.RightGrip) - body.position);
                MeasuredGripOverreach = MeasuredGripSpan - armLength;
            }

            if (figure.LeftLowerArm != null && figure.RightLowerArm != null)
            {
                MeasuredOffArmAbove =
                    figure.LeftLowerArm.position.y - figure.RightLowerArm.position.y;
                MeasuredArmGap =
                    Vector3.Distance(figure.LeftLowerArm.position, figure.RightLowerArm.position);
            }
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
        /// <summary>Gather one hand's grip bones. Any of them may be absent on a given rig.</summary>
        static HandGrip.Bones GripBones(Animator animator, bool right) => new HandGrip.Bones
        {
            Hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand),
            ThumbProximal = animator.GetBoneTransform(
                right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal),
            ThumbIntermediate = animator.GetBoneTransform(
                right ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate),
            ThumbDistal = animator.GetBoneTransform(
                right ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal),
            IndexProximal = animator.GetBoneTransform(
                right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal),
            IndexIntermediate = animator.GetBoneTransform(
                right ? HumanBodyBones.RightIndexIntermediate : HumanBodyBones.LeftIndexIntermediate),
            IndexDistal = animator.GetBoneTransform(
                right ? HumanBodyBones.RightIndexDistal : HumanBodyBones.LeftIndexDistal),
            MiddleProximal = animator.GetBoneTransform(
                right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal),
            MiddleIntermediate = animator.GetBoneTransform(
                right ? HumanBodyBones.RightMiddleIntermediate : HumanBodyBones.LeftMiddleIntermediate),
            MiddleDistal = animator.GetBoneTransform(
                right ? HumanBodyBones.RightMiddleDistal : HumanBodyBones.LeftMiddleDistal),
        };

        void BindWorkBones(Figure figure, Animator animator)
        {
            if (!animator.isHuman) return;

            figure.Spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            figure.RightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            figure.RightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            figure.LeftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            figure.LeftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            figure.LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            figure.RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            // The legs, which nothing bound until the crouch needed them. ApplyClimbPose's own
            // comment records their absence as a deliberate limit rather than an oversight —
            // "legs would be better and are not available without binding four more bones, which
            // is a piece of work rather than a tweak". This is that piece of work.
            figure.Hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            figure.LeftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            figure.LeftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            figure.LeftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            figure.RightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            figure.RightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            figure.RightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            // The fingers, so a hand can close on a haft instead of having one pass through it.
            // The Polygon rig maps thumb, index and middle at three joints each; no ring or little,
            // which at this camera height is not a difference anybody can see. See HandGrip.
            figure.RightGrip = GripBones(animator, right: true);
            figure.LeftGrip = GripBones(animator, right: false);

            // How tall this particular figure's hips stand, measured off its own rig rather than
            // named as a number. Sixty-one characters have sixty-one sets of proportions and the
            // director scales them besides, so a crouch expressed in metres is a deep squat on one
            // colonist and a curtsey on the next. Expressed as a fraction of this, it is the same
            // crouch on all of them.
            figure.StandingHipHeight = figure.Hips != null
                ? Mathf.Max(0.2f, figure.Hips.position.y - figure.Transform.position.y)
                : 0f;

            // And how long its legs are, for the same reason and measured the same way: a climber's
            // foothold is a fraction of its own leg, never a number of metres. Thigh plus shin
            // rather than hip-to-floor, because that is the quantity TwoBoneIk can actually deliver
            // and hip height includes an ankle and a boot that it cannot.
            figure.LegLength =
                figure.LeftUpperLeg != null && figure.LeftLowerLeg != null && figure.LeftFoot != null
                    ? Vector3.Distance(figure.LeftUpperLeg.position, figure.LeftLowerLeg.position)
                      + Vector3.Distance(figure.LeftLowerLeg.position, figure.LeftFoot.position)
                    : 0f;

            Transform? hand = figure.RightHand;
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

            // Into the palm, and *not* onto the hand bone. A humanoid hand bone is the wrist, so
            // seating the haft on it put the tool behind the hand — every hand in this project has
            // been holding its axe by the wrist since there was an axe, and the fingers reach past
            // it rather than round it. HandGrip.Palm says where a held thing really sits, measured
            // off the knuckles now that the fingers are bound.
            axe.position += HandGrip.Palm(figure.RightGrip) - axe.TransformPoint(grip);

            // Where the off hand takes hold, and where the edge is. Both are wanted every frame
            // afterwards — one to put the second fist on the haft, one to know how far this
            // figure can reach — so they are worked out once, here, in the axe's own space.
            fitted.OffHandGrip = butt + haft * (length * Mathf.Clamp01(look.ButtFraction));
            fitted.BladeTip = bounds.center + haft * half;

            // And the haft itself, because the working hand no longer sits in one place on it. The
            // fit is done once — it strikes a pose, measures the mesh and solves a reach — but
            // where the fist grips changes every frame, so the three numbers the slide needs are
            // kept rather than being thrown away with the local variables that held them.
            fitted.Butt = butt;
            fitted.Haft = haft;
            fitted.HaftLength = length;

            // And how the whole thing lies in the hand, which is the answer every later frame
            // re-derives rather than adjusts. See PlaceTool.
            fitted.Seat = axe.localRotation;
            fitted.Seated = true;
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
    }
}
