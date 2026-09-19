#nullable enable
using System;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Lateral steering envelope and kinematics for pawns manoeuvring around obstacles
    /// and passing oncoming pawns within the 2.5 m tile boundary.
    ///
    /// The simulation path remains strictly cell-to-cell. Presentation applies a smooth
    /// lateral deflection to the rendered figure so that colonists veer naturally to
    /// the side of a tile around trees, item heaps, or oncoming traffic.
    /// </summary>
    public static class SteeringCurve
    {
        /// <summary>
        /// Maximum allowable lateral offset from cell centreline (meters).
        /// At 0.60 m, two passing pawns maintain 1.20 m clearance while leaving
        /// 0.65 m clearance to the 2.5 m corridor walls (tile half-width is 1.25 m).
        /// </summary>
        public const float MaxLateralOffset = 0.60f;

        /// <summary>
        /// Hard clamp ceiling for any lateral displacement to guarantee a figure never
        /// escapes its tile bounds or clips into adjacent wall geometry.
        /// </summary>
        public const float HardClampedMax = 0.75f;

        /// <summary>
        /// Symmetric C^1 smooth bell curve B(t) = 16 * t^2 * (1 - t)^2 for t in [0, 1].
        /// Peaks at exactly 1.0 at t = 0.5.
        /// Value and first derivative are exactly 0 at t = 0 and t = 1, ensuring
        /// zero velocity/acceleration jerk when entering or leaving a cell.
        /// </summary>
        public static float Bell(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            float term = t * (1f - t);
            return 16f * term * term;
        }

        /// <summary>
        /// Derivative of the bell curve B'(t) = 32 * t * (1 - t) * (1 - 2t).
        /// Exactly 0 at t = 0, t = 0.5, and t = 1.
        /// </summary>
        public static float BellDerivative(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            return 32f * t * (1f - t) * (1f - 2f * t);
        }

        /// <summary>
        /// Smoothstep curve S(t) = 3t^2 - 2t^3 for t in [0, 1].
        /// Transitions smoothly from 0.0 at t=0 to 1.0 at t=1 with zero derivatives at both ends.
        /// </summary>
        public static float SmoothStep(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Anticipatory lead-in curve: stays 0 in [0, 0.5], then smoothly ramps to 1.0 in [0.5, 1.0].
        /// Used when the upcoming cell contains an obstacle so the figure starts veering
        /// before crossing the cell boundary.
        /// </summary>
        public static float AnticipatoryLeadIn(float t)
        {
            if (t <= 0.5f) return 0f;
            float u = (t - 0.5f) * 2f;
            return SmoothStep(u);
        }

        /// <summary>
        /// Lead-out curve: smoothly ramps down from 1.0 to 0.0 in [0, 0.5], then stays 0 in [0.5, 1.0].
        /// </summary>
        public static float LeadOut(float t)
        {
            if (t >= 0.5f) return 0f;
            float u = t * 2f;
            return 1f - SmoothStep(u);
        }

        /// <summary>
        /// Right-hand lateral normal orthogonal to horizontal travel heading.
        /// In Unity (X right, Z forward, Y up), for heading (dx, 0, dz),
        /// the right-hand normal is (dz, 0, -dx).
        /// </summary>
        public static Vector3 LateralRight(Vector3 heading)
        {
            float magSq = heading.x * heading.x + heading.z * heading.z;
            if (magSq < 1e-6f) return Vector3.zero;
            float invMag = 1f / Mathf.Sqrt(magSq);
            float dx = heading.x * invMag;
            float dz = heading.z * invMag;
            return new Vector3(dz, 0f, -dx);
        }

        /// <summary>
        /// How far short of square-on two paths start counting as converging, and how far past it
        /// they count as fully opposed. Cosines of the angle between the two headings.
        /// </summary>
        public const float ConvergingFrom = 0.2f;

        /// <summary>See <see cref="ConvergingFrom"/>.</summary>
        public const float OpposedBy = 0.6f;

        /// <summary>The range at which another colonist starts to be given room, in metres.</summary>
        public const float CrowdFarRadius = 3.0f;

        /// <summary>The range at which room-giving is at its full width, in metres.</summary>
        public const float CrowdNearRadius = 1.5f;

        /// <summary>
        /// How much room another colonist at this distance is given: 0 at
        /// <see cref="CrowdFarRadius"/> and beyond, 1 at <see cref="CrowdNearRadius"/> and inside,
        /// smoothly between. Continuous at both ends, which is the whole point — the hard 3.0 m
        /// cut-off it replaces moved a figure 0.6 m sideways between one tick and the next.
        /// </summary>
        public static float Proximity(float distance) =>
            SmoothStep((CrowdFarRadius - distance) / (CrowdFarRadius - CrowdNearRadius));

        /// <summary>
        /// Where a colonist is at this instant, interpolated along the step it is taking, or its
        /// cell centre when it is standing. Three-dimensional: a colonist on the storey above is
        /// three metres away, not nought (<c>CellMetrics.SizeY</c>).
        /// </summary>
        public static Vector3 WhereItIsNow(in PawnView pawn)
        {
            Vector3 at = CellMetrics.FloorCentre(pawn.Cell);
            if (!pawn.Moving) return at;
            float s = Mathf.Clamp01(pawn.MovePerMille > 0
                ? pawn.MovePerMille * 0.001f
                : pawn.MovePercent * 0.01f);
            return at + (CellMetrics.FloorCentre(pawn.NextCell) - at) * s;
        }

        /// <summary>
        /// How much this other colonist counts as being in the way, 0 to 1.
        ///
        /// <para>A colonist standing still is wholly in the way. One that is moving is in the way
        /// to the extent that it is coming the other way: nothing at all when it walks the same
        /// line as you, ramping in from square-on to head-on. It replaces a <c>dot &lt; -0.5</c>
        /// test, which switched the entire sidestep on and off as the other colonist turned.</para>
        ///
        /// <para>The ramp opens just <i>before</i> square-on and is only fully open past
        /// <c>0.6</c>, so crossing traffic gets about a sixth of the envelope and head-on gets all
        /// of it. Crossing was given nothing at all before, and it is the case a player is most
        /// likely to be looking at — two colonists converging on one doorway. Starting a little
        /// below square-on rather than exactly at it is what keeps two colonists on gently
        /// converging lines from ignoring each other until the last moment.</para>
        /// </summary>
        public static float InTheWay(Vector3 headingDir, in PawnView other)
        {
            if (!other.Moving) return 1f;

            Vector3 otherHeading = CellMetrics.FloorCentre(other.NextCell) - CellMetrics.FloorCentre(other.Cell);
            otherHeading.y = 0f;
            if (otherHeading.sqrMagnitude < 1e-6f) return 1f;

            float opposed = -Vector3.Dot(headingDir, otherHeading.normalized);
            return SmoothStep((opposed + ConvergingFrom) / (OpposedBy + ConvergingFrom));
        }

        /// <summary>
        /// Computes the lateral displacement vector for mutual passing between two pawns.
        /// Uses the right-hand rule (+sign) scaled by the passing bell envelope.
        /// </summary>
        public static Vector3 PassingDisplacement(Vector3 heading, float progress, float maxOffset = MaxLateralOffset)
        {
            float magnitude = Mathf.Clamp(maxOffset, 0f, HardClampedMax) * Bell(progress);
            return LateralRight(heading) * magnitude;
        }

        /// <summary>
        /// Computes the lateral displacement vector to veer around an in-cell obstacle (e.g. tree trunk or item heap).
        /// Follows the C^1 smooth bell curve B(progress) strictly within the obstacle cell so that
        /// entry (s=0) and exit (s=1) boundaries have zero displacement and zero velocity jerk,
        /// eliminating boundary snaps, speed observation spikes, and animation flickering.
        /// </summary>
        public static Vector3 ObstacleDisplacement(Vector3 heading, float progress, float maxOffset = MaxLateralOffset)
        {
            float magnitude = Mathf.Clamp(maxOffset, 0f, HardClampedMax) * Bell(progress);
            return LateralRight(heading) * magnitude;
        }

        /// <summary>
        /// Backwards-compatible overload for obstacle steering. Only applies displacement within the obstacle cell,
        /// ensuring zero offset across tile boundaries to guarantee C^1 visual continuity.
        /// </summary>
        public static Vector3 ObstacleDisplacement(Vector3 heading, float progress, bool isObstacleCell, bool isUpcomingObstacle = false, float maxOffset = MaxLateralOffset)
        {
            if (!isObstacleCell) return Vector3.zero;
            return ObstacleDisplacement(heading, progress, maxOffset);
        }
    }
}
