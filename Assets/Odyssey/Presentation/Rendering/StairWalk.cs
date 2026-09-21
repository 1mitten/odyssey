#nullable enable

using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where a figure is drawn while it climbs the colony's one-cell stair: <b>on the flights</b>,
    /// round the landing, and up the other side.
    ///
    /// <para><b>Why anything is needed.</b> The stair's connector joins a cell to the cell directly
    /// above it — the ladder's shape — so the step a pawn takes is <c>(0, +3, 0)</c> and nothing
    /// else. Drawn by the ordinary rule that lerps between two cell centres, a colonist rises
    /// vertically <i>through the middle of the staircase</i>: no forward motion, so no gait, no
    /// contact with anything, and the art passing through her. The owner, 2026-09-21, having asked
    /// for the one-cell stair the day before: <i>"the animation for going upstairs is terrible it
    /// needs to ground 2 flights of stairs and make sure the feet get onto each step and surface as
    /// it climbs."</i></para>
    ///
    /// <para><b>The path is measured off the art, not invented.</b>
    /// <c>SM_Bld_Base_Stairs_02</c> is a <b>switchback</b>: a flight along the south half of the
    /// cell climbing toward −X, a landing at the −X end, and a second flight along the north half
    /// climbing back toward +X. <c>StairCheck</c> bins the module's placed vertices over the cell
    /// footprint and prints the top surface; <see cref="Path"/> is the walking line read off that
    /// grid, and <c>StairWalkTests</c> checks every point of it against the same measurements. The
    /// alternative — hard-coding a guess from the play camera — is what put the flight at
    /// <c>yaw = 180</c> and drew it descending into the ground the day before.</para>
    ///
    /// <para><b>Smooth, not strided, and that is a decision with history.</b> The obvious reading of
    /// "the feet get onto each step" is to quantise the height to the sixteen treads. <b>Do not.</b>
    /// <see cref="HopArc"/> records that strides were built for the terrace climb and then taken out
    /// at the owner's own request — <i>"a consistently slow speed from top to bottom … it jolts and
    /// jitters the colonists at certain points; smoother is preferred and predictable"</i> — because
    /// four deliberate jolts a climb is what a stride rhythm is. A 0.1875 m riser snapped per tread
    /// would be seven times the 25 mm an honest frame of walking moves, sixteen times a flight. The
    /// walking line is the <b>nosing</b> — the ramp through the step edges, which is the surface a
    /// stair actually presents to a foot — so the feet are on the treads and nothing jolts.</para>
    ///
    /// <para><b>Nothing here is simulated.</b> The connector, its price and its two ends are the
    /// simulation's; this only decides where the figure is between them, exactly as
    /// <see cref="HopArc"/>, <see cref="BankLayout"/> and <see cref="GroundRelief"/> do. It cannot
    /// move a pawn, change a path or touch the state hash.</para>
    /// </summary>
    public static class StairWalk
    {
        /// <summary>
        /// The walking line, in the stair's own <b>facing-0</b> frame: metres from the cell centre
        /// in x and z, metres above the cell floor in y. Ascending order, foot of the flight first.
        ///
        /// <para><b>The two ends are cell centres and that is deliberate.</b> The step before this
        /// one finishes at <c>FloorCentre(stairCell)</c> and the step after begins at
        /// <c>FloorCentre(cell above)</c>; a path that began at the foot of the flight instead would
        /// tear at both joins, which is the class of fault <c>PawnPose</c> already carries three
        /// comments about. So the first and last segments are the short walk on to and off the
        /// flight, and every join is continuous by construction.</para>
        ///
        /// <para>Read off the surface grid in <c>StairCheck</c> (2026-09-21). The flights each
        /// occupy half the cell in z and run the full width in x; the landing is the strip from
        /// x = −1.25 to about x = −0.35, at exactly half a layer.</para>
        /// </summary>
        public static readonly Vector3[] Path =
        {
            new Vector3( 0.00f, 0.00f,  0.00f),   // the cell centre, where the walk in ends
            new Vector3( 1.05f, 0.05f, -0.60f),   // the foot of the lower flight
            new Vector3(-0.45f, Landing, -0.60f), // the top of the lower flight
            new Vector3(-0.80f, Landing,  0.00f), // the turn, in the middle of the landing
            new Vector3(-0.45f, Landing,  0.60f), // the foot of the upper flight
            new Vector3( 1.05f, CellMetrics.SizeY, 0.60f), // the top of the upper flight
            new Vector3( 0.00f, CellMetrics.SizeY, 0.00f), // the centre of the cell above
        };

        /// <summary>
        /// The landing's height: exactly half a layer, which is what makes the two flights equal.
        ///
        /// <para>Measured at 1.50 m on the art and stated as the arithmetic rather than as the
        /// measurement, because it is the arithmetic that has to hold: sixteen risers of 0.1875 m,
        /// eight to a flight. If a future piece of stair art disagrees, <c>StairWalkTests</c> fails
        /// on the surface samples rather than this line being quietly wrong.</para>
        /// </summary>
        public const float Landing = CellMetrics.SizeY * 0.5f;

        /// <summary>
        /// Whether this step is a climb or a descent of a colony-built stair, and which way.
        ///
        /// <para>Asked of the <b>lower</b> cell, because that is where the edifice stands: going up,
        /// the pawn is in it; going down, the pawn is in the cell above it. Worldgen's stamped
        /// two-cell stairwells answer false and keep the old drawing — they are a different thing
        /// and their halves lie side by side, so this path would be nonsense on one.</para>
        /// </summary>
        public static bool Crosses(WorldRenderModel? world, CellRef a, CellRef b,
            out CellRef stairCell, out bool up)
        {
            stairCell = default;
            up = false;
            if (world == null) return false;

            // One layer apart and directly over one another. Anything else is not this connector.
            if (a.X != b.X || a.Z != b.Z) return false;
            if (Mathf.Abs(a.Y - b.Y) != 1) return false;

            up = b.Y > a.Y;
            stairCell = up ? a : b;

            GridSize size = world.Size;
            if (!size.Contains(stairCell.X, stairCell.Z, stairCell.Y)) return false;

            return world.EdificeDef(size.Index(stairCell.X, stairCell.Z, stairCell.Y))
                   == CoreContent.EdificeStairFull;
        }

        /// <summary>
        /// Where the figure is, and which way it is facing, at <paramref name="t"/> of the way
        /// through the step.
        ///
        /// <para><b>Time is spread over the path, not over the point count</b> — the same rule
        /// <c>PawnPose.StepPace</c> applies to a terrace, and for its reason. The landing is flat
        /// and the flights climb, so giving each segment an equal share of the clock would cross
        /// the landing at a crawl and take the flights at a run. Each segment is weighted by its
        /// ground length plus <see cref="HopArc.ClimbWeight"/> metres for every metre of rise,
        /// which is one steady speed from bottom to top — <i>"consistently slow … smoother is
        /// preferred and predictable"</i>.</para>
        ///
        /// <para>The heading is the segment's own ground direction, so the figure turns on the
        /// landing rather than climbing the second flight backwards. It has no vertical part, for
        /// the reason <c>PawnPose</c> flattens every heading: a bearing is a bearing.</para>
        /// </summary>
        public static Vector3 At(CellRef stairCell, int facing, bool up, float t, out Vector3 heading)
        {
            float clamped = Mathf.Clamp01(t);

            // Descending is the same line walked backwards. One path, one set of numbers: a second
            // copy for the way down is how the two would come to disagree about where the landing
            // is, which is the fault this file's own StairShape sibling was written to prevent.
            float u = up ? clamped : 1f - clamped;

            float total = 0f;
            for (int i = 0; i + 1 < Path.Length; i++) total += Weight(i);

            float want = u * total;
            int seg = 0;
            float within = 0f;
            for (; seg + 1 < Path.Length; seg++)
            {
                float w = Weight(seg);
                if (want <= w || seg + 2 == Path.Length)
                {
                    within = w > 1e-5f ? want / w : 0f;
                    break;
                }
                want -= w;
            }

            Vector3 a = Path[seg], b = Path[seg + 1];
            Vector3 local = Vector3.Lerp(a, b, Mathf.Clamp01(within));

            Vector3 ground = new Vector3(b.x - a.x, 0f, b.z - a.z);
            if (!up) ground = -ground;

            float yaw = Directions.Yaw[facing & 3];
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

            heading = turn * ground;

            Vector3 world = CellMetrics.FloorCentre(stairCell) + turn * local;
            return world;
        }

        /// <summary>
        /// What a segment costs in time: its ground length, plus the climbing weight for its rise.
        /// Never zero, so a purely vertical segment could not swallow the whole clock.
        /// </summary>
        static float Weight(int i)
        {
            Vector3 a = Path[i], b = Path[i + 1];
            float ground = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            float rise = Mathf.Abs(b.y - a.y);
            return Mathf.Max(1e-4f, ground + HopArc.ClimbWeight * rise);
        }
    }
}
