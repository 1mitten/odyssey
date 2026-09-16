#nullable enable
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where a pawn is drawn this frame, and which way it is facing.
    ///
    /// The simulation is discrete: a pawn occupies one cell and retires integer cost units towards
    /// the next. Determinism requires that and it is not negotiable. Everything here is the facade
    /// over it — the glide between cells, and the extra fraction of a cell the pawn would have
    /// covered in the part-tick this frame sits in.
    ///
    /// It lives on its own because two things now need it: the instanced pass that draws a crowd
    /// as baked meshes, and the live figures that walk. Two copies of this arithmetic would look
    /// identical until the day they disagreed, and the symptom — a pawn's animated figure lagging
    /// half a cell behind where the game thinks it is — would be blamed on the animation.
    /// </summary>
    public static class PawnPose
    {
        /// <summary>
        /// The pawn's drawn position and heading.
        ///
        /// <paramref name="tickAlpha"/> is how far this frame sits between two ticks, 0 to 1, and
        /// <paramref name="movePerTick"/> the cost units a pawn retires in one tick, so a frame
        /// that lands between ticks can carry the pawn on rather than waiting. The extrapolation
        /// is clamped to the cell being entered: running past it would read as a stumble.
        ///
        /// <paramref name="heading"/> is zero for a pawn that is standing still, which callers
        /// must treat as "keep facing wherever you were" rather than as "face north".
        /// </summary>
        public static Vector3 Of(in PawnView pawn, float tickAlpha, int movePerTick, out Vector3 heading)
        {
            Vector3 from = CellMetrics.FloorCentre(pawn.Cell);
            if (pawn.MovePercent <= 0)
            {
                heading = Vector3.zero;
                return GroundRelief.Lift(from);
            }

            Vector3 to = CellMetrics.FloorCentre(pawn.NextCell);

            // The heading is taken between the flat cell centres and stays horizontal, which is
            // what every caller already assumes: it is a facing, and a pawn walking uphill faces
            // along the ground rather than up into the air.
            heading = to - from;
            float percent = pawn.MovePercent + movePerTick * tickAlpha;

            // The lift is taken at the interpolated position, not at either end, so a pawn walks
            // along the drawn ground instead of cutting the chord between two cell centres.
            return GroundRelief.Lift(from + heading * (Mathf.Clamp(percent, 0f, 100f) * 0.01f));
        }

        /// <summary>The yaw a heading implies, in degrees. Zero-length headings give zero.</summary>
        public static float YawOf(Vector3 heading) =>
            heading.sqrMagnitude > 1e-4f ? Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg : 0f;
    }
}
