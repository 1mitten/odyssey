#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Where a climber's feet go, as arithmetic.
    ///
    /// <para><b>Why the feet are solved and not authored,</b> which is the same argument the crouch
    /// made and is worth repeating because the two cases look different and are not.
    /// <c>13-gestures.md</c> §3: author the angles when the figure is aiming at something whose
    /// position we do not know, solve to a point when it has to meet something whose position we
    /// do. An axe stroke is the first case. A climb is the second — the figure is turned to face
    /// the wall and leaned against it before any of this runs, so the rock is a known plane in
    /// front of the hips, and the only question left is how far down it each boot is. Authored knee
    /// angles would have to be authored sixty-one times besides, once per character, because the
    /// packs' proportions differ and the director scales them on top.</para>
    ///
    /// <para><b>Everything here is a fraction of the figure's own leg</b>, never a number of metres.
    /// A foothold 0.7 m below the hip is a deep step on one colonist and a stumble on the next.</para>
    ///
    /// <para>Separate from <see cref="PawnFigureDirector"/> so it can be tested without a rig, a
    /// world or an animator, the same bargain <see cref="WorkSwing"/> and <see cref="Gesture"/>
    /// make.</para>
    /// </summary>
    public static class ClimbPose
    {
        /// <summary>
        /// How far below the hip the pushing boot goes, as a fraction of the leg's length.
        ///
        /// <para>Not one, and not even <see cref="Fits"/>: a leg driven to full extension
        /// straightens, and a straight leg on a wall reads as hanging rather than as pushing. It is
        /// also where <see cref="TwoBoneIk"/> stops being able to tell a solved pose from an
        /// unreachable one, which is the distinction <c>MeasuredFootReach</c> exists to keep.</para>
        /// </summary>
        public const float ExtendedDrop = 0.92f;

        /// <summary>How far below the hip the stepped boot goes. The other end of the cycle.</summary>
        public const float SteppedDrop = 0.68f;

        /// <summary>
        /// The most of its leg a climber is ever asked for, as a fraction. Whatever else gives, the
        /// boot stays on the rock: see <see cref="Foothold"/>.
        /// </summary>
        public const float Fits = 0.97f;

        /// <summary>
        /// How high each foot is in its cycle, 0 pushing at full stretch and 1 stepped up with the
        /// knee drawn in, given the arm swing that <see cref="PawnFigureDirector"/> already
        /// computes.
        ///
        /// <para><b>Contralateral, and that is the whole of what makes it read as climbing.</b>
        /// A person on a ladder or a rock face moves the opposite arm and leg together: the right
        /// hand goes up with the left foot. Move the same side's pair together and the figure
        /// bounds up the wall like a frog, which is a real gait for a real animal and not one for a
        /// colonist. So the left foot follows the right arm and the right foot the left arm, which
        /// is one subtraction and the reason this function exists rather than the number being
        /// inlined twice with a sign error waiting in it.</para>
        ///
        /// <paramref name="swing"/> is the director's own sine, -1 to 1, where +1 is the right arm
        /// at full reach overhead.
        /// </summary>
        public static void StepsFrom(float swing, out float left, out float right)
        {
            left = Mathf.Clamp01((swing + 1f) * 0.5f);
            right = 1f - left;
        }

        /// <summary>
        /// Where one boot is put, given the hip it hangs from and where the rock is.
        ///
        /// <para>Measured from the leg's <em>own</em> hip joint rather than from the pelvis, which
        /// is not tidiness: the two hip joints are already a stance apart, so taking each one as its
        /// own origin gives the feet their spacing for nothing, and gives it correctly on a narrow
        /// rig and a broad one alike.</para>
        ///
        /// <para><b>A wall is a plane, and the first version of this forgot it.</b> The step was
        /// written as a reach and an angle — shorter and further forward as the knee came up — so
        /// the two boots sat at different distances from the rock, one of them a good thirty
        /// centimetres inside it. Nothing in a wall-less contact sheet shows that, and on a real
        /// shaft it is the difference between standing on stone and standing in it. So the rock
        /// distance is held fixed and <em>only the height varies</em>: both boots lie on one plane,
        /// which is what a wall is.</para>
        ///
        /// <para><b>The boot stays on the rock and the height gives.</b> When the two together ask
        /// for more leg than there is — a short-legged rig, or a lean that put the wall further out
        /// than usual — it is the drop that is shortened, never the rock distance. A boot that has
        /// come up too high is a small step; a boot that has come off the wall is a colonist
        /// levitating, which is the thing the lean was written to fix.</para>
        ///
        /// <para><b>The body is authoritative and the feet follow it</b>, which is the exact inverse
        /// of the crouch. Stooping, the boots are on the ground and the pelvis is the thing that may
        /// move; on a wall there is no ground under the boots at all, the drawn position is settled
        /// by the lean, and the feet go wherever the body has arrived.</para>
        ///
        /// <paramref name="step"/> 0 is the pushing leg, 1 the stepped-up one.
        /// <paramref name="toRock"/> how far, and in which direction, the rock face stands from the
        /// figure — a distance and not a unit vector, because it is the lean that decides it.
        /// </summary>
        public static Vector3 Foothold(Vector3 hip, Vector3 toRock, Vector3 up, float legLength,
            float step)
        {
            legLength = Mathf.Max(0f, legLength);
            if (legLength <= 0f) return hip;

            Vector3 down = up.sqrMagnitude > 1e-8f ? -up.normalized : Vector3.down;
            float drop = Mathf.Lerp(ExtendedDrop, SteppedDrop, Mathf.Clamp01(step)) * legLength;

            // On to the rock first, and keep it: everything that gives, gives in the drop.
            float reach = toRock.magnitude;
            float room = legLength * Fits;
            if (reach >= room) return hip + toRock;

            drop = Mathf.Min(drop, Mathf.Sqrt(room * room - reach * reach));
            return hip + toRock + down * drop;
        }

    }
}
