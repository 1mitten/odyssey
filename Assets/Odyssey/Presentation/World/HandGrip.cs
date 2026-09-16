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
            Quaternion was = hand.rotation;

            float bestRoll = 0f;
            float bestDistance = float.MaxValue;

            // Sixteen samples round the circle. Finer buys nothing: the hand is then slerped
            // towards the answer by the grip weight, and a twenty-degree error in a fist at this
            // camera height is not a thing anybody can see.
            for (int step = 0; step < 16; step++)
            {
                float roll = step * (360f / 16f);
                hand.rotation = Quaternion.AngleAxis(roll, axis) * was;

                float distance = DistanceToLine(tip.position, haftPoint, axis);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestRoll = roll;
            }

            hand.rotation = was;
            Quaternion turn = Quaternion.AngleAxis(bestRoll, axis);
            Quaternion eased = Quaternion.Slerp(Quaternion.identity, turn, Mathf.Clamp01(amount));
            hand.rotation = eased * was;
            return eased;
        }

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
