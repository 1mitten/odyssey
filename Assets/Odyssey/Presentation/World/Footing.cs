#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// How a figure answers the ground it is drawn on: which way it leans, and where its feet go.
    ///
    /// <para><b>Why this is not nothing to do.</b> <c>GroundRelief</c> draws the board as a rolling
    /// field and then lifts everything standing on it straight up — "ground lies along a slope, but
    /// a person standing on a hillside stands up". That is the right rule for a <em>position</em>
    /// and it was never meant to be the whole answer: a figure lifted onto a slope and left bolt
    /// upright meets it on one heel, with the downhill foot in the air and the uphill one buried
    /// to the ankle. The lift puts the colonist in the right place; this puts it in the right
    /// attitude.</para>
    ///
    /// <para><b>All arithmetic, no bones.</b> Everything here is a pure function of a slope, a
    /// height or a pair of numbers, so it can be checked by a test rather than by looking at a
    /// screenshot — the house rule that <c>WorkSwing</c>, <c>GaitBlend</c> and <c>ArmIk</c> all
    /// follow. The director owns the <c>Transform</c> work; this owns the decisions.</para>
    ///
    /// <para><b>It is a facade, like the ground it answers to.</b> Nothing in the simulation knows
    /// a colonist is leaning. The pawn stays in its cell for picking, for the cursor and for every
    /// rule, exactly as <c>WorkStance</c> already arranges for a figure that steps up to a tree.
    /// Slope is not a cell property, so nothing here may change how fast anybody walks.</para>
    /// </summary>
    public static class Footing
    {
        /// <summary>
        /// How much of the ground's own tilt a figure takes, 0 to 1.
        ///
        /// <para>Not all of it, and the reason is anatomy rather than taste. A person on a hillside
        /// holds their head up and their weight over their feet; only their ankles really follow
        /// the slope. A figure rotated the full amount reads as a cardboard cut-out pasted onto the
        /// hill, which is the fault this is meant to fix, arriving from the other side.</para>
        /// </summary>
        public static float LeanFraction { get; set; } = 0.55f;

        /// <summary>
        /// The most a figure may lean, in degrees, however steep the ground beneath it.
        ///
        /// <para>The board's own field cannot reach this: at the shipped 2 m amplitude over a 150 m
        /// wavelength the steepest slope anywhere is 0.136 rise per metre, which is 7.7 degrees,
        /// and the lean takes a little over half of that. The cap is here for the surround, whose
        /// hills are far steeper, and for whatever the field becomes later — a number that is not
        /// currently binding is still the thing that stops a future tuning pass laying a colonist
        /// on its side.</para>
        /// </summary>
        public static float MaxLeanDegrees { get; set; } = 12f;

        /// <summary>
        /// How fast the lean settles, in degrees a second.
        ///
        /// <para>Eased for the same reason the bearing is (<c>TurnDegreesPerSecond</c>): a pawn
        /// crossing a ridge would otherwise change attitude between one frame and the next, which
        /// at this camera height reads as the figure flinching. Faster than the turn, because a
        /// lean is a small angle and a slow one lags visibly behind the feet.</para>
        /// </summary>
        public static float LeanDegreesPerSecond { get; set; } = 220f;

        /// <summary>
        /// How far a foot may be moved to meet the ground, in metres.
        ///
        /// <para>A band, not a limit, and the distinction is the whole of why running still looks
        /// like running. A walk cycle lifts the swing foot well clear of the ground on purpose; a
        /// solver that dragged every foot down to the surface would plant both of them and the
        /// colonist would skate. So the correction is full strength for a foot already near the
        /// ground and fades to nothing for one that is deliberately in the air.</para>
        /// </summary>
        public static float ReachMetres { get; set; } = 0.32f;

        /// <summary>
        /// The most an ankle may turn to lay a foot along the ground, in degrees.
        ///
        /// <para>An ankle has a limit and a foot that exceeds it reads as a broken one. The figure
        /// takes <see cref="LeanFraction"/> of the slope through its whole body, so the ankle is
        /// only ever asked for the remainder — but the remainder on a steep piece of surround is
        /// still more than a person has.</para>
        /// </summary>
        public static float MaxAnkleDegrees { get; set; } = 18f;

        /// <summary>Reset to the shipped values. For tests, which must not inherit each other's tuning.</summary>
        public static void Reset()
        {
            LeanFraction = 0.55f;
            MaxLeanDegrees = 12f;
            LeanDegreesPerSecond = 220f;
            ReachMetres = 0.32f;
            MaxAnkleDegrees = 18f;
        }

        /// <summary>
        /// The upward normal of a surface with these slopes, as a rise per metre along each axis.
        ///
        /// <para>The slopes come from <c>GroundRelief.SlopeAt</c>, which differentiates the field
        /// exactly rather than sampling it — so there is no raycast, no collider and no terrain
        /// here, and the answer agrees with the drawn ground to the last decimal instead of to
        /// whatever step a finite difference happened to use.</para>
        /// </summary>
        public static Vector3 GroundNormal(float slopeX, float slopeZ) =>
            new Vector3(-slopeX, 1f, -slopeZ).normalized;

        /// <summary>
        /// The world-space tilt a figure standing on that normal should carry, after the fraction
        /// and the cap have had their say.
        ///
        /// <para>Applied on the <em>left</em> of the figure's bearing by the caller, so that the
        /// figure yaws in the world and then leans with the hill. Composing the other way round
        /// leans it in its own frame, which turns the lean into a roll as it walks in a circle.</para>
        /// </summary>
        public static Quaternion LeanTo(Vector3 normal)
        {
            if (normal.sqrMagnitude < 1e-8f) return Quaternion.identity;

            Quaternion full = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            Quaternion wanted = Quaternion.Slerp(Quaternion.identity, full, Mathf.Clamp01(LeanFraction));

            // Cap by angle rather than by clamping the normal, because the cap is a statement about
            // the figure and not about the ground: it has to hold whatever field is underneath.
            return Quaternion.RotateTowards(Quaternion.identity, wanted, Mathf.Max(0f, MaxLeanDegrees));
        }

        /// <summary>One frame of easing from the lean a figure has towards the one it wants.</summary>
        public static Quaternion Settle(Quaternion current, Quaternion target, float deltaTime) =>
            Quaternion.RotateTowards(current, target, Mathf.Max(0f, LeanDegreesPerSecond * deltaTime));

        /// <summary>
        /// How far to move a foot, given where the gait put it and where the ground is.
        ///
        /// <para>Positive lifts the foot, negative sets it down. Full strength within half the
        /// reach, then faded smoothly to nothing by the full reach, so a foot that is genuinely in
        /// the air — mid-stride, mid-run — is left exactly where the clip put it. The fade is
        /// smoothstepped rather than linear because a linear one has a corner in it, and a corner
        /// in a correction is a visible tick as the foot passes through it.</para>
        /// </summary>
        public static float Correction(float footY, float groundY)
        {
            float gap = groundY - footY;
            float reach = Mathf.Max(1e-4f, ReachMetres);
            float magnitude = Mathf.Abs(gap);

            if (magnitude >= reach) return 0f;

            float half = reach * 0.5f;
            if (magnitude <= half) return gap;

            float t = Mathf.Clamp01((magnitude - half) / (reach - half));
            return gap * (1f - t * t * (3f - 2f * t));
        }

        /// <summary>
        /// How far the hips drop so neither leg has to over-reach.
        ///
        /// <para><b>This is the single thing that makes slope footing read.</b> Solve two legs
        /// independently on a hillside and the downhill one straightens, locks and still falls
        /// short, because a leg is only so long — the figure ends up doing the splits or floating.
        /// Lowering the whole body by the deepest reach any foot needs turns that into an ordinary
        /// bend, which is what a person actually does going downhill.</para>
        ///
        /// <para>Only ever down. Raising the hips to meet a foot that needs lifting would take the
        /// other foot off the ground with it, which trades one floating foot for another and adds
        /// a bob to every step across a slope.</para>
        /// </summary>
        public static float HipDrop(float leftCorrection, float rightCorrection) =>
            Mathf.Min(0f, Mathf.Min(leftCorrection, rightCorrection));

        /// <summary>
        /// The extra world-space turn that lays a foot along the ground, given the lean the body is
        /// already carrying.
        ///
        /// <para>The body has taken <see cref="LeanFraction"/> of the slope, so a foot hanging off
        /// it is already part of the way there and asking it for the whole tilt again would tip it
        /// past the ground. What is left is the full ground rotation with the body's own taken back
        /// out — then capped, because an ankle has a limit.</para>
        ///
        /// <para>World space on purpose, like <c>PawnFigureDirector.Pitch</c>: a bone's local axes
        /// belong to whoever rigged the character, and sixty-one characters from four packs are not
        /// a promise that any two agree about which way a foot points.</para>
        /// </summary>
        public static Quaternion AnkleLevel(Vector3 normal, Quaternion lean)
        {
            if (normal.sqrMagnitude < 1e-8f) return Quaternion.identity;

            Quaternion remainder = Quaternion.FromToRotation(Vector3.up, normal.normalized) *
                                   Quaternion.Inverse(lean);
            return Quaternion.RotateTowards(Quaternion.identity, remainder, Mathf.Max(0f, MaxAnkleDegrees));
        }
    }
}
