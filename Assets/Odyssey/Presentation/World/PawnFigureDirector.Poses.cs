#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
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
    /// director on 2026-09-16 because the one file had reached 3,033 lines; it is the same class
    /// and the same behaviour, and a member here may call one in any other part.</para>
    ///
    /// <para>The governing rule is <c>13-gestures.md</c> §3: <b>author the angles when the figure
    /// aims at something whose position we do not know; solve to a point when it must meet
    /// something whose position we do.</b></para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>
        /// The height of a figure's ankle above the ground it is standing on, in the idle pose.
        ///
        /// <para>The figure is placed with its root on the cell floor and the graph has just been
        /// evaluated into the idle, so both feet are down and the root is where the soles are.
        /// The ankle bone above it is therefore exactly the thickness of the boot, at whatever
        /// scale this face is drawn.</para>
        ///
        /// <para>Measured here rather than written down as a constant because the cast is
        /// sixty-one characters from four packs, drawn at 1.4, and a cowboy boot is not a
        /// trainer.</para>
        /// </summary>
        static float MeasureSole(Figure figure)
        {
            if (figure.LeftFoot == null || figure.RightFoot == null) return 0f;

            float ankle = Mathf.Min(figure.LeftFoot.position.y, figure.RightFoot.position.y);

            // Measured from the *posed mesh*, not from the root and not from the bounds.
            //
            // The first version took the root as the sole, which is true of a booted character and
            // not of a barefoot one: those kept sinking, because the root sits where a boot would
            // have been and a bare heel is higher. The second asked the renderer for its bounds
            // and got 0.394 m, because a SkinnedMeshRenderer's bounds are the loose precomputed
            // volume rather than the posed mesh. Baking the pose and reading the lowest vertex is
            // the only one of the three that answers the question actually being asked, and it
            // gets right whatever the next pack does -- the cast is sixty-one characters from four
            // packs and nothing says their rigs were built to one convention.
            float lowest = LowestDrawnPoint(figure);

            float sole = lowest < float.MaxValue
                ? ankle - lowest
                : ankle - figure.Transform.position.y;   // nothing bakeable: the old assumption

            // And a little more, so nothing grazes. A sole measured exactly right still leaves the
            // foot touching the ground at a single plane, and the drawn ground is not a plane --
            // GroundRelief shears every cell and the turf mesh is not flat inside one. A couple of
            // centimetres is below the threshold at which a figure reads as floating and above the
            // one at which the toe of a bare foot catches on the grass.
            sole += Footing.SoleClearance;

            // An absurd answer means the rig is not built the way this assumes, or the pose was
            // never evaluated. Better to correct nothing than to lift a colonist into the air on a
            // bad measurement.
            // A quarter of a metre is already a tall boot at this scale. Anything past it means
            // the rig is not built the way this assumes, and correcting nothing beats lifting a
            // colonist into the air on a bad measurement -- which is exactly what the bounds
            // version would have done.
            return sole > 0f && sole < 0.25f ? sole : 0f;
        }

        /// <summary>
        /// The world height of the lowest vertex this figure actually draws, in its current pose.
        ///
        /// <para>Baked rather than read off the renderer: <c>SkinnedMeshRenderer.bounds</c> is a
        /// conservative volume, not the posed mesh, and using it measured a sole of 0.394 m on a
        /// figure whose real one is a tenth of that. The bake is a script-created mesh, so it is
        /// readable whatever the source model's import settings say, and it happens once per
        /// figure at bind rather than per frame.</para>
        /// </summary>
        static float LowestDrawnPoint(Figure figure)
        {
            FigureBuild.DrawnExtent(figure.Skins, out float lowest, out _);
            return lowest;
        }

        /// <summary>
        /// How long a body this figure has to lay down when it sleeps: its own drawn height, sole
        /// to crown, measured once at bind off the posed mesh.
        ///
        /// <para><b>This replaces deriving it from the hip, which was measuring the floor.</b>
        /// <c>SleepPose.BodyLength</c> used to be <c>StandingHipHeight * 1.9</c>, and
        /// <see cref="Figure.StandingHipHeight"/> is <c>hips.position.y - transform.position.y</c>
        /// where the Synty avatar maps <c>Hips</c> to a bone named <c>Root</c> at the model
        /// origin. Nought on all sixty-one, clamped up to 0.2 m, and a 2.49 m colonist was laid
        /// down 0.38 m long — so her feet landed near the pillow and the rest of her hung two and
        /// a half metres off the head end of the bed. <c>docs/design/20-beds.md</c> §7b.</para>
        /// </summary>
        static float MeasureBody(Figure figure) =>
            FigureBuild.Height(figure.Skins, figure.Transform.position.y, FigureBuild.FallbackHeight);

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

                // A swimmer has no feet on the ground, and the ground is the bed of the stream a
                // metre and a half below. Left at full strength this would solve both legs down to
                // it and drag the figure back under the water it is floating on.
                //
                // **Faded, not switched.** The first version skipped the whole pass while the swim
                // weight was above a threshold, which meant the footing arrived complete on one
                // frame as a colonist left the water — hips dropping and both feet planting between
                // one frame and the next, every time anybody came ashore. That is a candidate for
                // the snap the owner reported on climbing out (2026-09-17). Scaling the correction
                // by how much of a walker the figure is makes the hand-over continuous, and at full
                // swim weight it multiplies to nothing and the early return below skips the solve
                // just as the old branch did.
                //
                // **A sleeper is not planted either**, and for a plainer reason than a swimmer: its
                // feet are on a mattress two thirds of a metre above the floor, and the ground this
                // pass solves against is the floor. Left in, the correction hauls both boots down
                // to the boards and drops the hips to follow — a colonist folded through its own
                // bed. Faded rather than switched, exactly as the swim is, so getting up hands the
                // footing back continuously instead of planting both feet on one frame.
                //
                // **Nor a body the knock-down clip has put on its back** (design 33 §1): its boots
                // are in the air by the clip's own authority, and planting them would drag it
                // upright by the ankles.
                // **Nor a body in the air over a stream** (design 46 §7): the footing would reach
                // for the ground under the gap, which is water a layer down.
                float planted = 1f - Mathf.Clamp01(Mathf.Max(
                    Mathf.Max(Mathf.Max(figure.SwimWeight, figure.SleepWeight), figure.AirWeight),
                    figure.Fight.Unplanted * figure.Fight.Weight));

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
                // The ankle goes a sole's height *above* the ground, not on it. Without the
                // offset the boot is buried to the ankle, which is what this pass was doing to
                // every colonist it corrected.
                float leftGround = figure.GroundY + GroundRelief.HeightAt(leftAt.x, leftAt.z) + figure.SoleOffset;
                float rightGround = figure.GroundY + GroundRelief.HeightAt(rightAt.x, rightAt.z) + figure.SoleOffset;

                float left = Footing.Correction(leftAt.y, leftGround) * planted;
                float right = Footing.Correction(rightAt.y, rightGround) * planted;
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
            SwimmingFigures = 0;
            SleepingFigures = 0;
            CarryingFigures = 0;
            MeasuredSwimPitch = 0f;
            MeasuredToolDrift = 0f;
            AimingFigures = 0;

            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Pawn < 0) continue;

                // An animal with a computed walk lays it over its idle here and does nothing
                // else in this pass: it has no arms to swing, no gestures, and does not sleep,
                // swim or climb yet (design 29). Idempotent for the reason the work pose is.
                if (figure.Gait != null)
                {
                    figure.Gait.Apply(figure.Transform.right, figure.Transform.up);
                    continue;
                }

                // A colonist cannot be swinging a pick and climbing at the same time, and the two
                // poses write the same bones, so the climb is applied here rather than in a pass
                // of its own — one place where an additive pose is laid over the clip, in one
                // order, for ever.
                if (figure.WorkWeight <= 0.001f)
                {
                    // Down behind cover first (design 53 §8a): the hips and legs only, so the aim
                    // laid on below takes the arms and the spine from a crouched body. Not over a
                    // pose that already owns the legs or the root — lying, swimming, climbing, or a
                    // stoop of its own.
                    if (figure.CoverCrouchWeight > 0.001f && figure.SleepWeight <= 0.001f
                        && figure.SwimWeight <= 0.001f && figure.ClimbPhase < 0f
                        && figure.Gesture == PawnGesture.None && !ForceGesture.HasValue)
                        ApplyCoverCrouch(figure);

                    // Sleep comes before all of them. Every other pose here describes a colonist
                    // on its feet — swimming, climbing, a one-shot gesture — and none of them
                    // means anything about a body that is lying down. It is also the only one
                    // that has already moved the root, so a pose laid on top of it would be
                    // writing arms onto a figure that is no longer where it thinks.
                    if (figure.SleepWeight > 0.001f)
                        ApplySleepPose(figure);
                    // Swimming comes first of the rest, because it is the only one that is a
                    // statement about where the colonist *is* rather than about what it is doing:
                    // a figure in the water is in the water whatever else it had in mind, and the
                    // other two poses both assume feet on the ground.
                    else if (figure.SwimWeight > 0.001f)
                        ApplySwimPose(figure);
                    else if (figure.ClimbPhase >= 0f && figure.ClimbFace != Vector3.zero)
                        ApplyClimbPose(figure);
                    // A computed blow, reaction or stun (design 33 §1): the fight owns the arms
                    // while it lasts, and the pack's clips need nothing here at all.
                    else if (ShowsComputedCombat(figure))
                        ApplyCombatPose(figure);
                    // The gun (design 47 §4b): the aim is a stance that owns both arms while it
                    // lasts, and low ready the right arm; after a blow or a stagger, which take the
                    // whole body for their moment, and before the one-shot gestures.
                    else if (figure.AimWeight > 0.001f)
                        ApplyAimPose(figure);
                    else if (figure.Gesture != PawnGesture.None || ForceGesture.HasValue)
                        ApplyGesturePose(figure);
                    else if (figure.LowReadyWeight > 0.001f)
                        ApplyLowReady(figure);
                    // Last of the five, and the only one that is a stance rather than an event.
                    // Everything above it either moves the whole body somewhere else (sleep, swim,
                    // climb) or is a motion that owns the arms for a moment (the lift, the stow),
                    // and a colonist doing any of those has no business also holding a pose.
                    else if (figure.CarryWeight > 0.001f)
                        ApplyCarryPose(figure);
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

            // **A second pass, and the separation is the whole correctness argument.** The cradle
            // is measured off the palms, and the palms are not where they will be drawn until
            // every branch above has had its say — the crouch that pulls them to the floor, the
            // work stance that takes one of them up a haft, the scoop that folds them in. Placing
            // inside the loop would read whichever bones that figure's branch happened to leave,
            // which is a load correct for a walking colonist and a frame late for a stooping one.
            for (int i = 0; i < _figures.Count; i++) PlaceCarriedLoad(_figures[i]);

            // And a third, for the same reason: a carried patient lies in her carrier's arms, and
            // the arms are final only now (design 33 §11e).
            PlaceCarriedPatients();
        }

        /// <summary>
        /// A downed colonist who is carried, or lying on a bed: drawn lying by the sleep pose rather
        /// than by the pack's floor loop, because the sleep pose can be aimed at a cradle or a
        /// mattress and the loop lies on whatever floor the root stands on (design 33 §11e).
        /// </summary>
        bool Cradled(in PawnView pawn)
        {
            if (!pawn.IsDowned) return false;
            if (pawn.IsCarried) return true;
            if (World == null || !World.Size.Contains(pawn.Cell.X, pawn.Cell.Z, pawn.Cell.Y)) return false;
            return World.BedHeadAt(World.Size.Index(pawn.Cell.X, pawn.Cell.Z, pawn.Cell.Y)) >= 0;
        }

        /// <summary>
        /// Whether this colonist has somebody in her arms: on a rescue, and the patient it names
        /// carried. The simulation says who carries whom from the patient's side
        /// (<c>Pawn.CarriedBy</c>), and publishes the rescuer's patient under an aspect of its own
        /// (design 33 §18b: the order target means an attack the player ordered, and nothing else).
        /// </summary>
        bool CarriesAPatient(in PawnView pawn)
        {
            if (_frame == null || pawn.JobDef != JobHandle.Rescue) return false;
            if (!_frame.TryGetPawnAspect(pawn.Id, Odyssey.Sim.Pawns.CombatAspects.RescuePatient, out int patient)) return false;
            return _frame.TryGetPawn(new PawnId(patient), out PawnView view) && view.IsCarried;
        }

        /// <summary>The figure of whoever is carrying <paramref name="patient"/>, if it has one.</summary>
        Figure? CarrierOf(PawnId patient)
        {
            if (_frame == null) return null;
            ReadOnlySpan<PawnView> pawns = _frame.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                if (pawns[i].JobDef != JobHandle.Rescue) continue;
                if (!_frame.TryGetPawnAspect(pawns[i].Id, Odyssey.Sim.Pawns.CombatAspects.RescuePatient, out int target)
                    || target != patient.Value) continue;
                return _byPawn.TryGetValue(pawns[i].Id.Value, out Figure? carrier) ? carrier : null;
            }
            return null;
        }

        /// <summary>
        /// Lay every carried patient across her carrier's arms (design 33 §11e, owner: cradled):
        /// the body's middle at the cradle measured off the carrier's palms — the load's own point,
        /// <see cref="CarryPose.Cradle"/> — lying at right angles to the way the carrier faces,
        /// head to her left. Only the root moves: the lying posture is already on the bones, which
        /// the root carries with it. A walk over the figures, asking the frame only of the carried.
        /// </summary>
        void PlaceCarriedPatients()
        {
            if (_frame == null) return;
            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Pawn < 0 || !_frame.TryGetPawn(new PawnId(figure.Pawn), out PawnView pawn) || !pawn.IsCarried)
                    continue;
                Figure? carrier = CarrierOf(pawn.Id);
                if (carrier == null || carrier.LeftGrip.Hand == null || carrier.RightGrip.Hand == null) continue;

                Vector3 left = HandGrip.Palm(carrier.LeftGrip);
                Vector3 right = HandGrip.Palm(carrier.RightGrip);
                Vector3 chest = carrier.Chest != null ? carrier.Chest.position : carrier.Transform.position;
                float shoulders = carrier.LeftUpperArm != null && carrier.RightUpperArm != null
                    ? Vector3.Distance(carrier.LeftUpperArm.position, carrier.RightUpperArm.position)
                    : 0f;
                Vector3 cradle = CarryPose.Cradle(left, right, chest, carrier.Transform.forward, shoulders);

                Vector3 across = Vector3.Cross(Vector3.up, carrier.Transform.forward);
                across.y = 0f;
                if (across.sqrMagnitude < 1e-6f) across = Vector3.right;
                across.Normalize();

                float half = SleepPose.BodyLength(figure.StandingHeight) * 0.5f;
                SleepPose.Place(
                    SleepPose.PostureFor(figure.Pawn), cradle - across * half, across,
                    cradle.y - CradleSink, figure.StandingHeight, 0f, 1f,
                    figure.Transform.position, figure.Transform.rotation,
                    out Vector3 lain, out Quaternion laid);
                figure.Transform.position = lain;
                figure.Transform.rotation = laid;
            }
        }

        /// <summary>
        /// How far below the cradle point the body's underside lies: the palms are under her, not
        /// level with her middle. INVENTED, for the playtest's eye.
        /// </summary>
        const float CradleSink = 0.06f;

        /// <summary>
        /// Fold the arms into the scoop: upper arms forward a little, elbows up, spine back.
        /// Design 24 §4.
        ///
        /// <para><b>Authored angles, not a solved target</b>, and the class remarks on
        /// <see cref="CarryPose"/> hold the argument. What a viewer reads at this camera is the
        /// posture, and the posture is the same on every rig only if the angles are.</para>
        ///
        /// <para>Scaled by the carry weight, exactly as the work pose is scaled by its own, so the
        /// arms fold in and let go over a fifth of a second instead of arriving. The spine's lean
        /// is <em>not</em> subtracted from the shoulders the way <c>Strike</c> subtracts it: there
        /// the three angles were being tuned against a photograph and had to mean three
        /// independent things, where here a spine that leans back and takes the shoulders with it
        /// is precisely what a person carrying a load does.</para>
        /// </summary>
        void ApplyCarryPose(Figure figure)
        {
            if (figure.RightUpperArm == null || figure.LeftUpperArm == null) return;

            float weight = Mathf.Clamp01(figure.CarryWeight);
            Vector3 axis = SwingAxis(figure.Transform, 0f);

            // **Stiff, and the stiffness is the point** (owner, 2026-09-19: the arms "should be
            // much stiffer and static held under the item rather than motioned because it's taken
            // weight it's holding"). Everything else in this director is *additive* over whatever
            // the walk clip gave, which is right for a gesture laid over a gait and wrong for a
            // stance — added to a swinging arm, a scoop is a scoop that swings, and the load
            // swings with it because the load follows the palms.
            //
            // So the arms are taken off the clip first, back to the rest the rig itself was
            // authored in, and the scoop is built from there. What is left moving is the torso
            // carrying them, which is what a person holding a weight in front of them looks like.
            //
            // **Written, not blended**, and that is what makes it safe to run twice in a frame:
            // ApplyWorkPose runs at the end of both Sync and Evaluate and only one of them
            // re-evaluates the graph first, so anything that eased towards a target from wherever
            // the bone happened to be would be integrated rather than recomputed — the fault that
            // made an axe spin. Assigning a constant is the identity on the second pass. The ease
            // therefore lives in the angles below and never in the rest.
            if (figure.RestArmsBound && weight > 0.001f)
            {
                figure.RightUpperArm.localRotation = figure.RestRightUpperArm;
                figure.LeftUpperArm.localRotation = figure.RestLeftUpperArm;
                if (figure.RightLowerArm != null)
                    figure.RightLowerArm.localRotation = figure.RestRightLowerArm;
                if (figure.LeftLowerArm != null)
                    figure.LeftLowerArm.localRotation = figure.RestLeftLowerArm;
            }

            // Back, not forward: the sign is the difference between carrying a weight and bowing
            // over it, and it is the one angle here where getting it wrong still looks deliberate.
            Pitch(figure.Spine, axis, -CarryPose.SpineLean * weight);

            Pitch(figure.RightUpperArm, axis, CarryPose.ShoulderPitch * weight);
            Pitch(figure.LeftUpperArm, axis, CarryPose.ShoulderPitch * weight);
            Pitch(figure.RightLowerArm, axis, CarryPose.ElbowBend * weight);
            Pitch(figure.LeftLowerArm, axis, CarryPose.ElbowBend * weight);

            // A box is gripped by its two sides, not scooped underneath (owner, 2026-09-25: the
            // medical kit). Out from the midline, about the figure's own forward axis, after the
            // scoop rather than instead of it — the same order SleepPose's ArmOut is laid over its
            // own pitch, and for the same reason.
            if (CarryPose.GrippedBySides(figure.CarryDef))
            {
                Vector3 outAxis = figure.Transform.forward;
                Pitch(figure.RightUpperArm, outAxis, -CarryPose.BoxGripOut * weight);
                Pitch(figure.LeftUpperArm, outAxis, CarryPose.BoxGripOut * weight);
            }

            CarryingFigures++;
        }

        /// <summary>
        /// Work out where this figure's load sits, from the palms it ended the frame with.
        ///
        /// <para><b>Absolute every frame, never adjusted.</b> The load is a prop and the animation
        /// graph has never heard of it, so this recomputes rather than nudges — the rule
        /// <c>PlaceTool</c> states after an axe spent a day winding itself into a spin because a
        /// world pose was written back as a local one, twice per frame, with nothing to converge
        /// to. Running this pass twice must leave the same answer, and a test asserts it.</para>
        ///
        /// <para>Nothing is drawn here. The placement is recorded on the figure and the renderer
        /// collects it, because the load belongs in the same instanced batch as the pile it came
        /// off — see <see cref="TryGetCarried"/>.</para>
        /// </summary>
        void PlaceCarriedLoad(Figure figure)
        {
            figure.CarryPlaced = false;

            if (figure.Pawn < 0 || figure.CarryDef < 0) return;

            // A rig with no hands bound is not an error — a non-Humanoid prefab answers null to
            // every bone and simply goes on walking, which is what the arms and the legs already
            // do for it. It carries nothing visible, which is better than carrying something at
            // the world origin.
            if (figure.LeftGrip.Hand == null || figure.RightGrip.Hand == null) return;

            // The owner's placeholder for the water case: a colonist afloat strokes with both arms
            // and a load left in them swings about. CarryPose.Drawn is the one place that decides.
            if (!CarryPose.Drawn(figure.SwimWeight)) return;

            // The palm, not the wrist. A humanoid hand bone is the wrist, and a load seated on two
            // wrists sits a hand's breadth behind where the arms actually hold it — the same
            // measurement the tool grip needed, taken by the same call.
            Vector3 left = HandGrip.Palm(figure.LeftGrip);
            Vector3 right = HandGrip.Palm(figure.RightGrip);

            // The chest is the centre line to measure clearance from. Falling back to the root is
            // not a degradation worth guarding: a rig with no chest bone has no torso mesh to
            // push the load out of either.
            Vector3 chest = figure.Chest != null ? figure.Chest.position : figure.Transform.position;

            float shoulders = figure.LeftUpperArm != null && figure.RightUpperArm != null
                ? Vector3.Distance(figure.LeftUpperArm.position, figure.RightUpperArm.position)
                : 0f;

            Vector3 cradle = CarryPose.Cradle(
                left, right, chest, figure.Transform.forward, shoulders);

            // Still arriving. Eased out — quick off the floor, slowing into the cradle — which is
            // a thing being lifted by somebody straightening up. See CarryHandover for why the
            // raise and the fall are deliberately different curves.
            figure.CarryAt = CarryHandover.RaiseFinished(figure.HandoverClock)
                ? cradle
                : Vector3.Lerp(figure.HandoverFrom, cradle,
                    CarryHandover.Raised(figure.HandoverClock));

            // **The load turns with the colonist.** Its own yaw, taken from the figure, because
            // the renderer's FacingOf is a memory of the last heading it *drew a stand-in at* and
            // it never records one for a pawn that has a live figure — so every load on a real
            // colonist was drawn at a yaw of exactly nought, and a log stayed pointing north
            // however she turned (owner, 2026-09-19).
            figure.CarryYaw = figure.Yaw;
            figure.CarryPlaced = true;
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
        /// <summary>
        /// A colonist in the water: pitched towards prone, pulling with alternate arms, legs
        /// trailing with a flutter.
        ///
        /// <para><b>The pitch is applied to the whole figure and the limbs on top of it</b>, in
        /// that order, because a swimmer is a walking pose tipped over rather than a new skeleton.
        /// Doing the limbs first and tipping afterwards gives the same picture only while the
        /// weight is exactly 1; part way in — which is every frame at the water's edge — the arms
        /// would swing in a plane that is not the one the body is in.</para>
        ///
        /// <para><b>Pitched about the hips and not the root.</b> The root is at the feet, so
        /// rotating about it would swing the whole body up out of the water like a hand on a clock
        /// face; the hips are near the middle of a person's mass and a body tips about them, which
        /// is the difference between floating and being levered.</para>
        ///
        /// <para><b>Everything is scaled by the weight</b>, so the pose arrives and leaves with
        /// the float rather than switching on. <see cref="WaterLine.Weight"/> blends over the step
        /// and <see cref="SwimPose.Settle"/> eases on top of that, which means a colonist walking
        /// into a stream tips over as it sinks in rather than at the moment its cell changes.</para>
        ///
        /// <para>The pose is arithmetic, in <see cref="SwimPose"/>, so the shape of the stroke is
        /// checkable by a test and only the application lives here — the bargain
        /// <see cref="ClimbPose"/> and <see cref="WorkSwing"/> already make.</para>
        /// </summary>
        void ApplySwimPose(Figure figure)
        {
            float weight = Mathf.Clamp01(figure.SwimWeight);
            float phase = HeldSwimPhase ?? SwimPose.Phase(figure.SwimClock);
            float swing = SwimPose.Swing(phase);

            // Tip the body. World-space about the figure's own right, for the same reason every
            // other pose here works in world space: a bone's local axes belong to whoever rigged
            // the character, and sixty-one characters from four packs are not a promise that any
            // two agree about which way is forward.
            if (figure.Hips != null)
            {
                Vector3 axis = figure.Transform.right;
                float degrees = SwimPose.PitchDegrees * weight;
                figure.Hips.rotation = Quaternion.AngleAxis(degrees, axis) * figure.Hips.rotation;
                MeasuredSwimPitch = Mathf.Max(MeasuredSwimPitch, degrees);
            }

            if (figure.RightUpperArm != null && figure.LeftUpperArm != null)
            {
                (float right, float left) = SwimPose.Arms(swing);
                Vector3 axis = SwingAxis(figure.Transform, 0f);

                Pitch(figure.RightUpperArm, axis, right * weight);
                Pitch(figure.RightLowerArm, axis, SwimPose.ElbowBend * weight);
                Pitch(figure.LeftUpperArm, axis, left * weight);
                Pitch(figure.LeftLowerArm, axis, SwimPose.ElbowBend * weight);
            }

            if (figure.RightUpperLeg != null && figure.LeftUpperLeg != null)
            {
                (float right, float left) = SwimPose.Legs(phase);
                Vector3 axis = SwingAxis(figure.Transform, 0f);

                Pitch(figure.RightUpperLeg, axis, right * weight);
                Pitch(figure.LeftUpperLeg, axis, left * weight);
            }

            SwimmingFigures++;
        }

        /// <summary>
        /// Lay a sleeper's limbs down, on top of whatever the standing idle clip is doing.
        ///
        /// <para><b>The clip underneath is a standing idle, and that is the whole problem this
        /// solves.</b> A standing idle holds the arms clear of the body and the legs apart, which
        /// once the figure is on its back is a colonist lying with its limbs out in the air. The
        /// root placement alone gets a plank; the limbs are what make it a person asleep.</para>
        ///
        /// <para>Which posture is <see cref="SleepPose.PostureFor"/>'s, derived from the pawn id so
        /// a colonist lies the same way every night and a barracks does not read as stamped.</para>
        /// </summary>
        void ApplySleepPose(Figure figure)
        {
            float weight = Mathf.Clamp01(figure.SleepWeight);
            SleepPose.Posture posture = SleepPose.PostureFor(figure.Pawn);
            Vector3 axis = SwingAxis(figure.Transform, 0f);

            if (figure.RightUpperArm != null && figure.LeftUpperArm != null)
            {
                Pitch(figure.RightUpperArm, axis, posture.RightArm * weight);
                Pitch(figure.LeftUpperArm, axis, posture.LeftArm * weight);

                // Out from the midline, about the body's forward axis, and after the pitch rather
                // than before it: the pitch decides where along the body the arm points and this
                // decides how far out from it, which is the order they read in. Mirrored by the
                // sign the caller gives each arm, because "out" is opposite on the two sides.
                Vector3 out_ = figure.Transform.forward;
                if (posture.RightArmOut != 0f) Pitch(figure.RightUpperArm, out_, posture.RightArmOut * weight);
                if (posture.LeftArmOut != 0f) Pitch(figure.LeftUpperArm, out_, posture.LeftArmOut * weight);

                Pitch(figure.RightLowerArm, axis, posture.RightElbow * weight);
                Pitch(figure.LeftLowerArm, axis, posture.LeftElbow * weight);
            }

            if (figure.RightUpperLeg != null && figure.LeftUpperLeg != null)
            {
                // The shared bend first, then the right leg's own on top of it: that is what
                // makes one knee drawn up and the other flat, and it is why Lead* is an extra
                // rather than a replacement.
                Pitch(figure.RightUpperLeg, axis, (posture.Hip + posture.LeadHip) * weight);
                Pitch(figure.LeftUpperLeg, axis, posture.Hip * weight);
                Pitch(figure.RightLowerLeg, axis, (posture.Knee + posture.LeadKnee) * weight);
                Pitch(figure.LeftLowerLeg, axis, posture.Knee * weight);
            }

            SleepingFigures++;
        }

        /// <summary>Figures laid down asleep this pass. Diagnostic, for tests and the overlay.</summary>
        public int SleepingFigures { get; private set; }

        /// <summary>Figures posed as swimmers this pass. Diagnostic, for tests and the overlay.</summary>
        public int SwimmingFigures { get; private set; }

        /// <summary>The deepest pitch applied to any swimmer this pass, in degrees. Diagnostic.</summary>
        public float MeasuredSwimPitch { get; private set; }

        /// <summary>
        /// Hold the swim stroke at one phase, for a contact sheet or a test that wants the same
        /// instant every run. Null lets the clock drive it, which is what the game does.
        /// </summary>
        public float? HeldSwimPhase { get; set; }

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

            // **A ladder is not a rock face, and the owner's reference photographs are the
            // evidence** (2026-09-18, five climbers from behind and one from the side). On stone a
            // climber takes whatever holds there are and the lower hand ends near its own hip; on a
            // ladder BOTH hands are on rungs above the head — the trailing one at about chin height
            // and the leading one at nearly full stretch — because the rungs are where they are and
            // there is nowhere else to put a hand. Reaching goes a little higher for the same
            // reason: a rung is gripped over the top, not pressed against.
            //
            // Two numbers, and they are the owner's to tune once they have watched a colonist go
            // up one. Nothing else in the pose depends on them.
            const float LadderReaching = -162f;
            const float LadderPulling = -104f;
            const float LadderElbowBend = -46f;

            float reaching = figure.OnLadder ? LadderReaching : Reaching;
            float pulling = figure.OnLadder ? LadderPulling : Pulling;
            float elbow = figure.OnLadder ? LadderElbowBend : ElbowBend;

            float swing = Mathf.Sin(figure.ClimbPhase * ReachesPerCell * 2f * Mathf.PI);

            if (figure.RightUpperArm != null && figure.LeftUpperArm != null)
            {
                float right = Mathf.Lerp(pulling, reaching, (swing + 1f) * 0.5f) * figure.ClimbWeight;
                float left = Mathf.Lerp(reaching, pulling, (swing + 1f) * 0.5f) * figure.ClimbWeight;

                // No tilt: a climb is straight up the sagittal plane, where a swing is across the
                // body.
                Vector3 axis = SwingAxis(figure.Transform, 0f);

                Pitch(figure.RightUpperArm, axis, right);
                Pitch(figure.RightLowerArm, axis, elbow * figure.ClimbWeight);
                Pitch(figure.LeftUpperArm, axis, left);
                Pitch(figure.LeftLowerArm, axis, elbow * figure.ClimbWeight);
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

            float steppedDrop = figure.OnLadder
                ? ClimbPose.LadderSteppedDrop : ClimbPose.SteppedDrop;

            PlantFoot(figure, figure.LeftUpperLeg, figure.LeftLowerLeg, figure.LeftFoot,
                toRock, left, steppedDrop);
            PlantFoot(figure, figure.RightUpperLeg, figure.RightLowerLeg, figure.RightFoot,
                toRock, right, steppedDrop);
        }

        /// <summary>One boot on to its hold, eased out of wherever the gait had it.</summary>
        void PlantFoot(Figure figure, Transform? upper, Transform? lower, Transform? foot,
            Vector3 toRock, float step, float steppedDrop)
        {
            if (upper == null || lower == null || foot == null) return;

            Vector3 hold = ClimbPose.Foothold(upper.position, toRock, figure.Transform.up,
                figure.LegLength, step, steppedDrop);
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
            kind == PawnGesture.Stow ? Gesture.Stow
            : kind == PawnGesture.Sow ? Gesture.Sow
            : Gesture.Lift;

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
        /// Down behind cover (design 53 §8a): the gesture stoop's own method — the pelvis down and a
        /// little back, each leg solved back to the foot the gait put down — held rather than
        /// played, and nothing done to the back or the arms, which are the aim's or the rest's.
        /// </summary>
        void ApplyCoverCrouch(Figure figure)
        {
            if (figure.Hips == null || figure.LegLength <= 0f) return;
            float depth = CoverCrouchLegFraction * figure.LegLength * figure.CoverCrouchWeight;
            if (depth <= 1e-4f) return;
            CrouchedFigures++;
            if (depth > MeasuredCrouchDrop) MeasuredCrouchDrop = depth;

            Vector3 leftFoot = figure.LeftFoot != null ? figure.LeftFoot.position : Vector3.zero;
            Vector3 rightFoot = figure.RightFoot != null ? figure.RightFoot.position : Vector3.zero;
            Quaternion leftSole = figure.LeftFoot != null ? figure.LeftFoot.rotation : Quaternion.identity;
            Quaternion rightSole = figure.RightFoot != null ? figure.RightFoot.rotation : Quaternion.identity;

            figure.Hips.position += Vector3.down * depth - figure.Transform.forward * (depth * 0.25f);
            Vector3 knee = figure.Transform.forward;
            SolveLeg(figure.LeftUpperLeg, figure.LeftLowerLeg, figure.LeftFoot, leftFoot, leftSole, knee);
            SolveLeg(figure.RightUpperLeg, figure.RightLowerLeg, figure.RightFoot, rightFoot, rightSole, knee);
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
        /// How much of a climbing step is spent letting go at the top, as a fraction.
        ///
        /// <para>A quarter of a layer, which at the ladder's cost is about a fifth of a second —
        /// long enough to read as reaching the ledge and short enough that most of the step is
        /// still a climb. See <see cref="ToppingOut"/>.</para>
        /// </summary>
        public const float TopTaper = 0.25f;

        /// <summary>
        /// How much climb is left in a step, 1 in the middle of it and 0 at the top end.
        ///
        /// <para><b>The top end, not the finish.</b> Going up that is the end of the step; going
        /// down it is the start, because a colonist stepping off a ledge on to a ladder is at the
        /// top of it on its first frame. Reading the phase without the direction would have the
        /// figure let go of the wall at the bottom of every descent, which is where it needs to
        /// hold on most.</para>
        ///
        /// <para>This is what stops the arms being overhead on the ledge: it runs the whole climb
        /// pose out — arms, legs and the lean together — over the last quarter of the rise, so the
        /// figure arrives standing rather than arriving and then unwinding (owner, 2026-09-18).</para>
        /// </summary>
        public static float ToppingOut(float phase, bool up)
        {
            if (phase < 0f) return 0f;

            float topness = up ? Mathf.Clamp01(phase) : 1f - Mathf.Clamp01(phase);
            if (topness <= 1f - TopTaper) return 1f;
            return Mathf.Clamp01((1f - topness) / TopTaper);
        }

        /// <summary>
        /// The direction of the ladder standing in this cell, or false when none does.
        ///
        /// <para><b>Asked before the wall, because a ladder is the thing you climb.</b> Where both
        /// exist they agree by construction — <see cref="WorldRenderModel.LadderFacing"/> fixes the
        /// ladder to the first occluding neighbour, which is the wall the old scan would have found
        /// anyway — and where only the ladder exists this is the difference between a colonist on
        /// a ladder and a colonist levitating beside one.</para>
        ///
        /// <para>The direction is the ladder's <em>back</em>: the mesher rotates the module so its
        /// front looks out along <c>LadderFacing</c>, so the climber faces the opposite way, into
        /// the rungs. One owner for that face and two readers of it, which is the whole point of
        /// asking the model rather than the neighbourhood.</para>
        /// </summary>
        bool TryLadderBeside(CellRef at, out Vector3 toLadder)
        {
            toLadder = Vector3.zero;
            WorldRenderModel? world = World;
            if (world == null) return false;

            int index = world.Size.Index(at.X, at.Z, at.Y);
            if (world.EdificeDef(index) != CoreContent.EdificeLadder) return false;

            int back = Directions.Opposite(world.LadderFacing(index));
            toLadder = new Vector3(Directions.DeltaX[back], 0f, Directions.DeltaZ[back]);
            return true;
        }

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

        /// <summary>
        /// Applies procedural gaze and head turning across all active figures.
        ///
        /// Runs after ApplyWorkPose at the end of both Sync and Evaluate.
        /// Uses 30% Neck and 70% Head distribution, resolving targets via the 6-tier
        /// priority arbiter with smooth damping and anatomical angle clamping.
        /// </summary>
        void ApplyGazePose(float deltaTime)
        {
            MeasuredGazeYaw = 0f;
            MeasuredGazePitch = 0f;
            ActiveGazePriority = GazePriority.None;

            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Pawn < 0 || figure.Head == null) continue;

                // Harness overrides
                if (ForceGazeAngles.HasValue)
                {
                    figure.Gaze.CurrentAngles = ForceGazeAngles.Value;
                    figure.Gaze.GazeWeight = 1f;
                    figure.Gaze.ActivePriority = ForceGazePriority ?? GazePriority.WorkFocus;
                }
                else if (ForceGazeTarget.HasValue)
                {
                    figure.Gaze.TargetWorldPosition = ForceGazeTarget.Value;
                    figure.Gaze.HasTarget = true;
                    figure.Gaze.GazeWeight = 1f;
                    figure.Gaze.ActivePriority = ForceGazePriority ?? GazePriority.WorkFocus;
                }

                Transform refFrame = figure.Transform;
                Vector3 headPivot = figure.Head.position;

                HeadLookKinematics.UpdateDampedAngles(ref figure.Gaze, refFrame, headPivot, deltaTime);

                Vector3 yawAxis = figure.Transform.up;
                Vector3 pitchAxis = figure.Transform.right;
                HeadLookKinematics.ApplyAdditiveRotation(
                    figure.Neck, figure.Head, yawAxis, pitchAxis, figure.Gaze.CurrentAngles, figure.Gaze.GazeWeight);

                float absYaw = Mathf.Abs(figure.Gaze.CurrentAngles.y);
                float absPitch = Mathf.Abs(figure.Gaze.CurrentAngles.x);
                if (absYaw > Mathf.Abs(MeasuredGazeYaw)) MeasuredGazeYaw = figure.Gaze.CurrentAngles.y;
                if (absPitch > Mathf.Abs(MeasuredGazePitch)) MeasuredGazePitch = figure.Gaze.CurrentAngles.x;
                if (figure.Gaze.ActivePriority > ActiveGazePriority) ActiveGazePriority = figure.Gaze.ActivePriority;
            }
        }
    }
}
