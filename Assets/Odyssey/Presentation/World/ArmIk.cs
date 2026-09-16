#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Reach an arm to a point: the two-bone inverse-kinematics solve, and nothing else.
    ///
    /// **Why a figure needs one at all.** The axe swing poses bones by angle, which works for the
    /// hand that holds the tool and cannot work for the hand that has to *meet* it. Shoulders are
    /// the better part of half a metre apart, so no pair of angles applied to the left arm will
    /// ever bring the left fist to a haft held in the right: they put it in a plausible attitude
    /// about forty centimetres to the side, which reads as two people doing the same dance and
    /// one of them holding nothing. The second fist has to reach for the haft, and reaching is
    /// this.
    ///
    /// It is the ordinary analytic solve — two bones, a target, the law of cosines — rather than
    /// anything iterative, because two bones have a closed form and an iterative solver would be
    /// slower, less predictable and no more correct. It is written against transforms instead of
    /// an animation rig because it runs after the animation has been written, in the same pass as
    /// the rest of the work pose; see <c>PawnFigureDirector.Strike</c>.
    ///
    /// The same call will hold the other end of a stretcher, a crate carried between two people,
    /// or a beam being lifted into place, which is why it is its own file rather than six lines
    /// inside the swing.
    /// </summary>
    public static class ArmIk
    {
        /// <summary>
        /// Bend <paramref name="upper"/> and <paramref name="lower"/> so that
        /// <paramref name="hand"/> lands on <paramref name="target"/>.
        ///
        /// <paramref name="poleHint"/> is a point the elbow is turned away from, which settles the
        /// one degree of freedom the law of cosines leaves open: the whole arm can spin about the
        /// line from shoulder to target, and without a hint it spins to wherever the previous pose
        /// happened to leave it, so a figure's elbow flicks inside out between frames. Passing a
        /// point behind the figure keeps elbows pointing outward and down, as elbows do.
        ///
        /// A target further away than the arm is long is not an error and is not reported as one:
        /// the arm straightens towards it and stops, which is what an arm does. Any missing bone
        /// leaves the pose untouched.
        /// </summary>
        public static void Reach(Transform? upper, Transform? lower, Transform? hand,
            Vector3 target, Vector3 poleHint)
        {
            if (upper == null || lower == null || hand == null) return;

            Vector3 shoulder = upper.position;
            Vector3 elbow = lower.position;
            Vector3 wrist = hand.position;

            float armLength = Vector3.Distance(shoulder, elbow);
            float forearmLength = Vector3.Distance(elbow, wrist);
            if (armLength < 1e-4f || forearmLength < 1e-4f) return;

            // Just short of straight and just short of folded, both ends. A fully straight arm has
            // no bend plane left to speak of and the next frame's solve has nothing to work from;
            // a fully folded one puts the fist through the shoulder.
            float span = Mathf.Clamp(Vector3.Distance(shoulder, target),
                Mathf.Abs(armLength - forearmLength) + 0.01f,
                armLength + forearmLength - 0.01f);

            // The bend plane: through the shoulder, the target and the hint, so the elbow stays on
            // the far side from the hint however the arm is turned.
            Vector3 toTarget = target - shoulder;
            if (toTarget.sqrMagnitude < 1e-6f) return;

            Vector3 bend = Vector3.Cross(toTarget, poleHint - shoulder);
            if (bend.sqrMagnitude < 1e-6f)
            {
                // Target, shoulder and hint in a line. Any plane will do; take one that is at
                // least stable, from the current elbow.
                bend = Vector3.Cross(toTarget, elbow - shoulder);
                if (bend.sqrMagnitude < 1e-6f) return;
            }
            bend.Normalize();

            // The interior angle at the shoulder, by the law of cosines. That is the only angle
            // that has to be solved: once the elbow is in the right place the forearm is simply
            // aimed at the target, which lands the wrist on it exactly and, more to the point,
            // leaves no second rotation whose sign could be wrong. A sign error in an elbow is
            // both easy to make and hard to see — the hand still arrives, the joint just folds
            // backwards — so the arithmetic here is arranged to have only one.
            float atShoulder = Mathf.Acos(Mathf.Clamp(
                (armLength * armLength + span * span - forearmLength * forearmLength)
                / (2f * armLength * span), -1f, 1f)) * Mathf.Rad2Deg;

            // Straighten the whole arm at the target, then open it to the solved angle.
            Vector3 direction = toTarget.normalized;
            upper.rotation = Quaternion.FromToRotation(elbow - shoulder, direction) * upper.rotation;
            upper.rotation = Quaternion.AngleAxis(atShoulder, bend) * upper.rotation;

            lower.rotation = Quaternion.FromToRotation(
                hand.position - lower.position, target - lower.position) * lower.rotation;
        }
    }
}
