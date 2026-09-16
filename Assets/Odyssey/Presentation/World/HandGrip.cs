#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Close a hand round a haft.
    ///
    /// <para><b>Why this exists.</b> Both fists were put on the axe — the working hand by parenting
    /// the tool to it, the off hand by an inverse-kinematics reach — and neither of them ever
    /// closed. The fingers stayed in whatever the idle clip left them, which is open and relaxed,
    /// so the haft passed straight through two flat hands. From a distance it reads as a colonist
    /// balancing an axe rather than holding one (owner, 2026-09-16).</para>
    ///
    /// <para><b>It is possible at all because the Polygon rig maps fingers.</b> That was worth
    /// checking before writing anything: a fist is fingers, and had the humanoid stopped at the
    /// wrist — as plenty of low-poly rigs do — no rotation applied anywhere would have closed a
    /// hand, and the only honest answers would have been a different mesh or none.
    /// <c>Generic_Characters.fbx</c> maps forty bones including thumb, index and middle at three
    /// joints each on both hands. No ring or little finger, which does not matter: at this camera
    /// height three curled fingers and a thumb read as a fist.</para>
    ///
    /// <para><b>The curl axis is measured, not named.</b> Which way a finger bone's local axes
    /// point is a decision made by whoever rigged the character, exactly as it is for the arm
    /// bones that <c>PawnFigureDirector</c> deliberately pitches about the figure's own axis
    /// instead. So the axis across the palm is found from the hand's own geometry — the line from
    /// the index knuckle to the thumb knuckle — and the <em>sign</em> is found by trying both and
    /// keeping whichever actually brings the fingertip towards the palm. That is the same trick
    /// <see cref="TwoBoneIk"/> uses to place an elbow, and it is used for the same reason: a sign
    /// reasoned out from an axis name has been wrong every time it has been reasoned out in this
    /// project.</para>
    /// </summary>
    public static class HandGrip
    {
        /// <summary>
        /// One hand's bones: the wrist, and the three fingers the rig actually carries.
        ///
        /// A struct of nullables rather than an array, because every one of these is optional —
        /// a rig may map some and not others, and a clone without the packs has none at all.
        /// </summary>
        public struct Bones
        {
            public Transform? Hand;
            public Transform? ThumbProximal, ThumbIntermediate, ThumbDistal;
            public Transform? IndexProximal, IndexIntermediate, IndexDistal;
            public Transform? MiddleProximal, MiddleIntermediate, MiddleDistal;

            /// <summary>True when there is enough here to make anything like a fist.</summary>
            public bool Any => IndexProximal != null || MiddleProximal != null;
        }

        /// <summary>
        /// How far each joint comes round at a full grip, in degrees.
        ///
        /// <para>The knuckle does most of it and the tip least, which is what a hand closed on a
        /// thick haft does — a fist closed on nothing curls the tips hardest, and that is the pose
        /// that looks wrong holding a tool. The thumb is much less and goes the other way, because
        /// it opposes rather than joins the others.</para>
        /// </summary>
        public const float KnuckleDegrees = 62f;
        public const float MiddleJointDegrees = 55f;
        public const float TipDegrees = 28f;
        public const float ThumbDegrees = 34f;

        /// <summary>
        /// Close the hand, <paramref name="amount"/> from 0 (open) to 1 (gripping).
        ///
        /// <para>Eased in with the work pose, so a colonist takes hold of the axe as it lifts it
        /// rather than snapping into a fist on one frame.</para>
        /// </summary>
        public static void Close(in Bones bones, float amount)
        {
            if (!bones.Any || bones.Hand == null || amount <= 0.001f) return;

            if (!TryCurlAxis(bones, out Vector3 axis, out float sign)) return;

            float knuckle = KnuckleDegrees * amount * sign;
            float middle = MiddleJointDegrees * amount * sign;
            float tip = TipDegrees * amount * sign;

            Curl(bones.IndexProximal, axis, knuckle);
            Curl(bones.IndexIntermediate, axis, middle);
            Curl(bones.IndexDistal, axis, tip);

            Curl(bones.MiddleProximal, axis, knuckle);
            Curl(bones.MiddleIntermediate, axis, middle);
            Curl(bones.MiddleDistal, axis, tip);

            // The thumb opposes the fingers: it comes round the other side of the haft, so it
            // turns the other way and much less far. Curled with the fingers it disappears into
            // the palm, which reads as a hand with something wrong with it.
            float thumb = ThumbDegrees * amount * -sign;
            Curl(bones.ThumbProximal, axis, thumb);
            Curl(bones.ThumbIntermediate, axis, thumb * 0.7f);
            Curl(bones.ThumbDistal, axis, thumb * 0.5f);
        }

        /// <summary>
        /// Where in the hand a held thing actually sits, in world space.
        ///
        /// <para><b>This is the bug the fists were really suffering from, and it predates them.</b>
        /// Everything that has ever put a tool in a hand here has put it at
        /// <c>hand.position</c> — and a humanoid hand bone is the <em>wrist</em>. The palm and the
        /// knuckles are five to eight centimetres further on, so the haft was seated behind the
        /// hand for as long as there has been an axe: the fingers are near it rather than round
        /// it, and no amount of curling them closes a fist on something that is not there. The
        /// owner saw it as "the hands aren't really near the actual axe" (2026-09-16), which is
        /// exactly what it is.</para>
        ///
        /// <para>It could not be computed before, because the fingers were not bound. Now that
        /// they are, the palm is a measurement rather than a guessed offset: it lies between the
        /// wrist and the knuckles, nearer the knuckles, which is where the hollow of a hand closed
        /// on a haft is. With no fingers mapped there is nothing to measure and the wrist is the
        /// best available answer — the same fallback as everywhere else here.</para>
        /// </summary>
        public static Vector3 Palm(in Bones bones)
        {
            Transform? hand = bones.Hand;
            if (hand == null) return Vector3.zero;

            Transform? index = bones.IndexProximal;
            Transform? middle = bones.MiddleProximal;
            if (index == null && middle == null) return hand.position;

            Vector3 knuckles = index != null && middle != null
                ? (index.position + middle.position) * 0.5f
                : (index != null ? index.position : middle!.position);

            return Vector3.Lerp(hand.position, knuckles, PalmFraction);
        }

        /// <summary>
        /// How far from the wrist towards the knuckles the haft sits, as a fraction.
        ///
        /// <para>Not a half: a haft held in a closed hand rests against the heads of the fingers
        /// rather than in the middle of the palm, so it sits nearer the knuckles. Under about a
        /// half it slides back towards the wrist and the fingers reach past it again, which is the
        /// state this whole change is fixing.</para>
        /// </summary>
        public const float PalmFraction = 0.7f;

        /// <summary>
        /// Turn the hand so its palm addresses the haft, before the fingers close on it.
        ///
        /// <para>Closing the fingers is only half of holding something. A hand whose palm faces the
        /// sky curls its fingers round thin air while the haft lies across the back of it, which is
        /// a worse picture than the open hand it replaced — the fingers now assert a grip that is
        /// visibly not there.</para>
        ///
        /// <para><b>Found by search rather than by convention.</b> Which way a hand bone's local
        /// axes point belongs to whoever rigged the character, and this project has been wrong
        /// every single time it has reasoned a sign or an axis out of a name. So the hand is rolled
        /// about the haft through a full circle, a few degrees at a time, and the roll kept is
        /// whichever brings the fingertips closest to the haft — which is what "the palm is facing
        /// it" means, stated as a distance so that it can be measured instead of argued.</para>
        ///
        /// <para>Returns the rotation it applied, so a caller holding a tool parented to this hand
        /// can put the tool back where it was. Rotating the hand rotates everything hanging off it,
        /// and the axe hangs off it.</para>
        /// </summary>
        public static Quaternion FaceHaft(in Bones bones, Vector3 haftPoint, Vector3 haftDirection,
            float amount)
        {
            if (!bones.Any || bones.Hand == null || amount <= 0.001f) return Quaternion.identity;
            if (haftDirection.sqrMagnitude < 1e-8f) return Quaternion.identity;

            Transform hand = bones.Hand;
            Transform? tip = bones.MiddleProximal ?? bones.IndexProximal;
            if (tip == null) return Quaternion.identity;

            Vector3 axis = haftDirection.normalized;

            // Which side of the wood the hand is coming from. **A position, and that is the whole
            // point**: the seat below is built out of this and the haft, and out of nothing that
            // depends on how the hand is currently turned. Positions here come from the inverse
            // kinematics, which solves to a target rather than adding to a pose, so the answer is
            // the same however many times it is asked for.
            Vector3 outward = hand.position - haftPoint;
            outward -= axis * Vector3.Dot(outward, axis);
            if (outward.sqrMagnitude < 1e-8f)
            {
                outward = Vector3.Cross(axis, Vector3.up);
                if (outward.sqrMagnitude < 1e-8f) outward = Vector3.Cross(axis, Vector3.right);
            }

            Quaternion was = hand.rotation;
            Quaternion seat = Seat(hand, tip, haftPoint, axis, outward.normalized);

            // Set, not eased. An ease would have to blend from the hand's current rotation, and
            // the current rotation is the previous answer — which is the loop this whole change
            // exists to cut. The grip is only asked for once the hand is at the wood anyway, and
            // the arm's own reach still eases, so what is lost is a fraction of a second of wrist
            // and what is gained is a hand that holds still.
            hand.rotation = seat;
            return seat * Quaternion.Inverse(was);
        }

        /// <summary>
        /// The one orientation in which this hand grips a haft: palm on the wood, fingers round it.
        ///
        /// <para><b>Absolute, and that is the entire difference from what this used to be.</b> The
        /// old version searched for the best roll <em>starting from wherever the hand happened to
        /// be</em> and then turned it partway there. Three passes of <see cref="Grasp"/> and two
        /// pose passes a frame meant six partial turns, each quantised to the sample spacing, none
        /// of them converging on anything — so the fist hunted round the haft and sometimes flipped
        /// to the far side of it, because "fingertips nearest the wood" is equally true of a palm on
        /// either side. The owner saw it as the left hand twisting strangely (2026-09-16), which is
        /// exactly what it was.</para>
        ///
        /// <para>Now the answer is a function of the haft and of which side the hand approaches
        /// from, and of nothing else. Ask twice, get the same rotation; ask six times, still the
        /// same. It is the same rule the tool's own <c>Seat</c> obeys, and the same rule the whole
        /// pose system rests on: <b>a pose may add to a bone, because the animation graph rewrites
        /// bones; it may never add to its own previous answer.</b></para>
        ///
        /// <para><b>The search is still a search</b>, because which way a hand bone's local axes
        /// point belongs to whoever rigged the character, and this project has been wrong every
        /// single time it has reasoned an axis out of a name. What changed is where it starts: from
        /// a frame built out of the haft rather than from the hand's own history. The refinement
        /// pass is there because a coarse sweep alone quantises the answer, and a grip that jumps
        /// twenty degrees as the haft turns past a sample boundary reads as a flinch.</para>
        /// </summary>
        static Quaternion Seat(Transform hand, Transform tip,
            Vector3 haftPoint, Vector3 axis, Vector3 outward)
        {
            Quaternion was = hand.rotation;

            // Along the wood, with the back of the hand outwards. Every candidate is this turned
            // about the haft, so none of them can be on the far side of it.
            Quaternion baseline = Quaternion.LookRotation(axis, outward);

            float best = 0f, bestDistance = float.MaxValue;
            for (int step = 0; step < Samples; step++)
                Consider(step * (360f / Samples), ref best, ref bestDistance);

            // And once more either side of the winner, so the answer moves smoothly with the haft
            // instead of stepping between samples. Off the coarse winner held still, not off the
            // running one: refining around a value the refinement is editing walks the search away
            // from where it meant to look.
            float coarse = best;
            float span = 360f / Samples;
            for (int step = -Refinements; step <= Refinements; step++)
                Consider(coarse + step * (span / (Refinements + 1)), ref best, ref bestDistance);

            hand.rotation = was;
            return Quaternion.AngleAxis(best, axis) * baseline;

            void Consider(float roll, ref float keptRoll, ref float keptDistance)
            {
                hand.rotation = Quaternion.AngleAxis(roll, axis) * baseline;
                float distance = DistanceToLine(tip.position, haftPoint, axis);
                if (distance >= keptDistance) return;
                keptDistance = distance;
                keptRoll = roll;
            }
        }

        /// <summary>How many rolls round the haft the coarse sweep tries.</summary>
        const int Samples = 16;

        /// <summary>How many finer steps either side of the coarse winner. See <see cref="Seat"/>.</summary>
        const int Refinements = 3;

        static float DistanceToLine(Vector3 point, Vector3 origin, Vector3 direction)
        {
            Vector3 offset = point - origin;
            return (offset - direction * Vector3.Dot(offset, direction)).magnitude;
        }

        /// <summary>
        /// The axis a finger turns about, and which way round it closes.
        ///
        /// <para>The axis is the line across the knuckles, taken from the two knuckles themselves
        /// so that it belongs to the rig rather than to an assumption. The sign is decided by
        /// experiment: rotate the middle knuckle one way, see whether its tip came nearer the
        /// wrist, and keep the answer. Curling is the direction that closes the hand, and "closes"
        /// is a measurable thing, so it is measured.</para>
        /// </summary>
        static bool TryCurlAxis(in Bones bones, out Vector3 axis, out float sign)
        {
            axis = Vector3.zero;
            sign = 1f;

            Transform? hand = bones.Hand;
            Transform? knuckle = bones.MiddleProximal ?? bones.IndexProximal;
            Transform? tip = bones.MiddleDistal ?? bones.IndexDistal ?? bones.MiddleIntermediate;
            if (hand == null || knuckle == null || tip == null) return false;

            // Across the palm. Index knuckle to thumb knuckle when both are there; otherwise fall
            // back to the hand's own sideways axis, which is a guess but a bounded one.
            if (bones.IndexProximal != null && bones.ThumbProximal != null)
                axis = bones.ThumbProximal.position - bones.IndexProximal.position;
            if (axis.sqrMagnitude < 1e-8f) axis = hand.right;
            if (axis.sqrMagnitude < 1e-8f) return false;
            axis = axis.normalized;

            // Which way is "closed"? Turn the knuckle a little each way and keep whichever brings
            // the fingertip nearer the wrist. Restored afterwards, so this costs a rotation and
            // changes nothing.
            Quaternion was = knuckle.rotation;

            knuckle.rotation = Quaternion.AngleAxis(20f, axis) * was;
            float positive = Vector3.Distance(tip.position, hand.position);

            knuckle.rotation = Quaternion.AngleAxis(-20f, axis) * was;
            float negative = Vector3.Distance(tip.position, hand.position);

            knuckle.rotation = was;
            sign = positive < negative ? 1f : -1f;
            return true;
        }

        static void Curl(Transform? bone, Vector3 axis, float degrees)
        {
            if (bone == null) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
