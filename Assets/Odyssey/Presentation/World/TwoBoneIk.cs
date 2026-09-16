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
    ///
    /// <para><b>And a leg is two bones.</b> It was called <c>ArmIk</c> until the crouch needed it,
    /// which was a name describing the first caller rather than the thing: a hip, a knee and a
    /// foot are the same chain as a shoulder, an elbow and a hand, with the pole in front instead
    /// of behind. That matters more for legs than it ever did for arms, because sixty-one
    /// characters have sixty-one sets of limb proportions — a crouch posed by authored angles is a
    /// deep squat on one figure and a curtsey on the next, where a crouch solved to the foot the
    /// gait already put down is right on all of them and needs no photograph.</para>
    /// </summary>
    public static class TwoBoneIk
    {
        /// <summary>
        /// Bend <paramref name="upper"/> and <paramref name="lower"/> so that
        /// <paramref name="hand"/> lands on <paramref name="target"/>.
        ///
        /// <paramref name="pole"/> is a point the elbow is sent **towards**, which settles the one
        /// degree of freedom the law of cosines leaves open: the whole arm can spin about the line
        /// from shoulder to target, and every position on that spin is an equally correct answer
        /// to "put the hand here". Without a pole it lands wherever the previous pose left it.
        ///
        /// It is a pole *target* and not a hint, and which of those two it is turned out to matter
        /// a great deal: sent the wrong way the elbow does not merely look odd, it goes through
        /// the ribs, and the whole forearm with it. Which way round the rotation runs depends on
        /// the handedness of the axis convention, so rather than reason about that — twice today
        /// a sign reasoned out that way has been wrong — the solve tries both and keeps whichever
        /// actually puts the elbow nearer the pole.
        ///
        /// A target further away than the arm is long is not an error and is not reported as one:
        /// the arm straightens towards it and stops, which is what an arm does. Any missing bone
        /// leaves the pose untouched.
        /// </summary>
        public static void Reach(Transform? upper, Transform? lower, Transform? hand,
            Vector3 target, Vector3 pole)
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

            // The bend plane: through the shoulder, the target and the pole.
            Vector3 toTarget = target - shoulder;
            if (toTarget.sqrMagnitude < 1e-6f) return;

            Vector3 toPole = pole - shoulder;
            Vector3 bend = Vector3.Cross(toTarget, toPole);
            if (bend.sqrMagnitude < 1e-6f)
            {
                // Target, shoulder and pole in a line. Any plane will do; take one that is at
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

            // Straighten the whole arm at the target, then open it to the solved angle — on
            // whichever side of the line actually puts the elbow towards the pole. Measured rather
            // than reasoned: the two candidates are one line apart and the wrong one buries the
            // elbow in the chest.
            Vector3 direction = toTarget.normalized;
            Vector3 acrossPole = Vector3.ProjectOnPlane(toPole, direction);
            float sign = 1f;
            if (acrossPole.sqrMagnitude > 1e-6f)
            {
                Vector3 one = Quaternion.AngleAxis(atShoulder, bend) * direction;
                Vector3 other = Quaternion.AngleAxis(-atShoulder, bend) * direction;
                sign = Vector3.Dot(one, acrossPole) >= Vector3.Dot(other, acrossPole) ? 1f : -1f;
            }

            upper.rotation = Quaternion.FromToRotation(elbow - shoulder, direction) * upper.rotation;
            upper.rotation = Quaternion.AngleAxis(atShoulder * sign, bend) * upper.rotation;

            lower.rotation = Quaternion.FromToRotation(
                hand.position - lower.position, target - lower.position) * lower.rotation;
        }
    }
}
